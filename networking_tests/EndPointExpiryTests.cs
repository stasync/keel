using Keel.Networking.Udp.LowLevel;
using System.Net;
using Xunit.Abstractions;

namespace Keel.Networking.Tests
{
    /// <summary>
    /// Per endpoint tracking state is allocated on the first datagram from any source address, before anything
    /// has authenticated. Endpoints that become connections are released explicitly by the layer above, but a
    /// scan, a spoofed source, or an abandoned handshake never gets that far - so the protocol expires its own
    /// state instead of relying on a caller to do it.
    /// </summary>
    public class EndPointExpiryTests : IDisposable
    {
        private const int TICK_MS = 10;

        /// <summary>
        /// Both the inactivity timeout and the sweep interval are MAX_RESEND_DURATION_MS, so expiry lands
        /// within one interval past the timeout - twice that leaves room for a slow tick.
        /// </summary>
        private const int EXPIRY_BUDGET_MS = (int)UdpTransport.MAX_RESEND_DURATION_MS * 4;

        private readonly ITestOutputHelper _output;

        private readonly int _receiverPort;
        private readonly UdpProtocol _receiver;
        private readonly UdpProtocol _oneShotSender = new(port: 0, protocolKey: 0);
        private readonly UdpProtocol _activeSender = new(port: 0, protocolKey: 0);

        public EndPointExpiryTests(ITestOutputHelper output)
        {
            _output = output;

            _receiverPort = Utils.GetAvailableUdpPort();
            _receiver = new UdpProtocol(_receiverPort, protocolKey: 0);
        }

        public void Dispose()
        {
            _receiver.Dispose();
            _oneShotSender.Dispose();
            _activeSender.Dispose();
        }

        [Fact]
        public void SilentEndPointsAreExpiredWhileActiveOnesAreKept()
        {
            var receiverEndPoint = new IPEndPoint(IPAddress.Loopback, _receiverPort);

            // One endpoint speaks once and goes quiet, the other keeps talking.
            Send(_oneShotSender, receiverEndPoint);
            Send(_activeSender, receiverEndPoint);

            PumpUntil(() => _receiver.TrackedEndPointCount == 2, millisecondsTimeout: 500, keepActiveSenderTalking: false);
            Assert.Equal(2, _receiver.TrackedEndPointCount);

            var startTime = DateTime.UtcNow;
            var expiredAfterMs = -1d;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < EXPIRY_BUDGET_MS)
            {
                Tick(keepActiveSenderTalking: true, receiverEndPoint);

                if (_receiver.TrackedEndPointCount > 1)
                    continue;

                expiredAfterMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                break;
            }

            _output.WriteLine($"expired after   : {expiredAfterMs:F0}ms");
            _output.WriteLine($"tracked endpoints: {_receiver.TrackedEndPointCount}");

            Assert.True(expiredAfterMs > 0,
                $"The silent endpoint was never expired - still tracking {_receiver.TrackedEndPointCount}.");

            // The one that kept sending is still tracked, so the sweep expires on silence and not on age.
            Assert.Equal(1, _receiver.TrackedEndPointCount);
        }

        private static void Send(UdpProtocol sender, IPEndPoint receiverEndPoint)
        {
            var writer = new NetWriter();
            writer.SeekZero();
            writer.WriteString("ping");

            sender.SendTo(receiverEndPoint, writer.AsArraySegment(), UdpProtocol.DeliveryMethod.Unreliable);
        }

        private void PumpUntil(Func<bool> condition, int millisecondsTimeout, bool keepActiveSenderTalking)
        {
            var startTime = DateTime.UtcNow;
            while (!condition() && (DateTime.UtcNow - startTime).TotalMilliseconds < millisecondsTimeout)
                Tick(keepActiveSenderTalking, receiverEndPoint: null);
        }

        private void Tick(bool keepActiveSenderTalking, IPEndPoint receiverEndPoint)
        {
            Thread.Sleep(TICK_MS);

            if (keepActiveSenderTalking && receiverEndPoint != null)
                Send(_activeSender, receiverEndPoint);

            _oneShotSender.Poll();
            _activeSender.Poll();
            _receiver.Poll();

            while (_receiver.TryDequeueIncoming(out _))
            {
            }
        }
    }
}