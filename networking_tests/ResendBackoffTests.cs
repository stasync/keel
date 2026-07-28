using Core.Networking.Udp.LowLevel;
using System.Net;
using Xunit.Abstractions;

namespace Core.Networking.Tests
{
    /// <summary>
    /// An unacked reliable datagram used to be retransmitted on every single Poll tick, so the cost of one
    /// lost datagram scaled with the caller's update rate rather than with the network. The retry delay now
    /// grows with the attempt counter instead.
    /// </summary>
    public class ResendBackoffTests : IDisposable
    {
        private const int TICK_MS = 10;

        /// <summary>
        /// Short of the give up horizon, so the backoff can be observed without the datagram being dropped
        /// part way through the measurement.
        /// </summary>
        private const int WINDOW_MS = 1500;

        /// <summary>
        /// Records when each datagram actually left the socket, at the lowest level, retransmissions included.
        /// </summary>
        private sealed class SendTimeRecorder : UdpReliableProtocol.DataTransferLayer
        {
            internal readonly List<DateTime> SendTimes = new();

            public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data) =>
                SendTimes.Add(DateTime.UtcNow);

            public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data)
            {
            }
        }

        private readonly ITestOutputHelper _output;

        private readonly UdpFullProtocol _sender;
        private readonly UdpFullProtocol _blackHole;
        private readonly SendTimeRecorder _senderSends = new();
        private readonly int _blackHolePort;

        public ResendBackoffTests(ITestOutputHelper output)
        {
            _output = output;

            _blackHolePort = Utils.GetAvailableUdpPort();

            // A live socket that never acknowledges anything. Dropping at the simulator rather than sending to
            // a closed port keeps ICMP unreachable out of the picture.
            _blackHole = new UdpFullProtocol(_blackHolePort, protocolKey: 0);
            _blackHole.RegisterIncomingPacketSimulator(new Udp.LowLevel.Simulators.DropAllPacketsSimulator());

            _sender = new UdpFullProtocol(port: 0, protocolKey: 0);
            _sender.RegisterDataTransferLayer(_senderSends);
        }

        public void Dispose()
        {
            _sender.Dispose();
            _blackHole.Dispose();
        }

        [Fact]
        public void UnackedDatagramIsRetriedOnAGrowingDelayRatherThanEveryTick()
        {
            var writer = new NetWriter();
            writer.SeekZero();
            writer.WriteString("payload that will never be acknowledged");

            _sender.SendTo(new IPEndPoint(IPAddress.Loopback, _blackHolePort), writer.AsArraySegment(),
                UdpFullProtocol.DgramDeliveryMethod.Reliable);

            var ticks = 0;
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < WINDOW_MS)
            {
                Thread.Sleep(TICK_MS);

                _sender.Poll();
                _blackHole.Poll();
                ticks++;
            }

            var gaps = new List<double>();
            for (var i = 1; i < _senderSends.SendTimes.Count; i++)
                gaps.Add((_senderSends.SendTimes[i] - _senderSends.SendTimes[i - 1]).TotalMilliseconds);

            _output.WriteLine($"ticks in window : {ticks}");
            _output.WriteLine($"datagrams sent  : {_senderSends.SendTimes.Count}");
            _output.WriteLine($"gaps (ms)       : {string.Join(", ", gaps.ConvertAll(gap => $"{gap:F0}"))}");

            // Retransmission is driven by elapsed time, not by how often the caller polls.
            Assert.True(_senderSends.SendTimes.Count < ticks / 4,
                $"Sent {_senderSends.SendTimes.Count} datagrams over {ticks} ticks - still resending per tick.");

            // The delay grows with the attempt counter, so later gaps are materially larger than earlier ones.
            Assert.True(gaps.Count >= 3, $"Expected at least 3 retransmissions to compare, got {gaps.Count}.");
            Assert.True(gaps[^1] > gaps[0] * 1.5,
                $"Retry delay did not grow: first gap {gaps[0]:F0}ms, last gap {gaps[^1]:F0}ms.");

            // Still inside the give up window, so nothing has been reported yet.
            Assert.Equal(0, _sender.ReliabilityFailureCount);
        }

        /// <summary>
        /// Retrying is bounded by elapsed time rather than by an attempt count, so the give up point does not
        /// move when the resend delays are retuned.
        /// </summary>
        [Fact]
        public void UnackedDatagramIsGivenUpOnOnceTheResendDurationElapses()
        {
            var writer = new NetWriter();
            writer.SeekZero();
            writer.WriteString("payload that will never be acknowledged");

            _sender.SendTo(new IPEndPoint(IPAddress.Loopback, _blackHolePort), writer.AsArraySegment(),
                UdpFullProtocol.DgramDeliveryMethod.Reliable);

            var sentTime = DateTime.UtcNow;
            var gaveUpAfterMs = -1d;

            while ((DateTime.UtcNow - sentTime).TotalMilliseconds < UdpReliableProtocol.MAX_RESEND_DURATION_MS * 2)
            {
                Thread.Sleep(TICK_MS);

                _sender.Poll();
                _blackHole.Poll();

                if (_sender.ReliabilityFailureCount <= 0)
                    continue;

                gaveUpAfterMs = (DateTime.UtcNow - sentTime).TotalMilliseconds;
                break;
            }

            var sendsAtGiveUp = _senderSends.SendTimes.Count;
            _output.WriteLine($"gave up after   : {gaveUpAfterMs:F0}ms");
            _output.WriteLine($"datagrams sent  : {sendsAtGiveUp}");

            Assert.True(gaveUpAfterMs > 0, "The datagram was never given up on.");

            // Bounded by the resend duration, with a tick of slack either side.
            Assert.InRange(gaveUpAfterMs, UdpReliableProtocol.MAX_RESEND_DURATION_MS - TICK_MS,
                UdpReliableProtocol.MAX_RESEND_DURATION_MS + TICK_MS * 10);

            // And it stops retransmitting once it has given up.
            for (var i = 0; i < 20; i++)
            {
                Thread.Sleep(TICK_MS);
                _sender.Poll();
            }

            Assert.Equal(sendsAtGiveUp, _senderSends.SendTimes.Count);
            Assert.Equal(1, _sender.ReliabilityFailureCount);
        }
    }
}