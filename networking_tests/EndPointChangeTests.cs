using Core.Networking.Udp;
using Core.Networking.Udp.LowLevel;
using Core.Utils.Debug;
using System.Net;

namespace Core.Networking.Tests
{
    /// <summary>
    /// A session can start arriving from a different endpoint - a NAT rebind, a Wi-Fi handover, a changed
    /// public address. Heartbeats keep working either way, because the reply goes back to whatever address the
    /// datagram arrived from, so the session looks healthy. But outgoing data keeps targeting the address
    /// captured at connect, so the client silently stops receiving while still appearing connected.
    ///
    /// The endpoint is deliberately not followed - doing so would let anyone knowing the connection uid,
    /// validation uid and slot redirect a session to their own address. So the log is the only signal.
    /// </summary>
    public class EndPointChangeTests : IDisposable
    {
        /// <summary>A single client on a fresh listener always lands in the first slot.</summary>
        private const byte FIRST_SLOT = 0;

        private readonly ReliableUdpListener _server;
        private readonly ReliableUdpClient _client = new();
        private readonly List<string> _capturedLogs = new();
        private readonly Action<LogLevel, string> _logHandler;

        public EndPointChangeTests()
        {
            _server = new ReliableUdpListener(maxConnections: 4, port: 0, protocolKey: 0);

            _logHandler = (_, message) =>
            {
                lock (_capturedLogs)
                    _capturedLogs.Add(message);
            };

            Logger.LogReceivedThreaded += _logHandler;
        }

        public void Dispose()
        {
            Logger.LogReceivedThreaded -= _logHandler;

            _server.Dispose();
            _client.Disconnect();
        }

        [Fact]
        public void SessionArrivingFromANewEndpointIsReportedButNotFollowed()
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));
            PumpUntil(() => _client.IsConnected, ReliableUdpListener.HEARTBEAT_TIMEOUT_MS);
            Assert.True(_client.IsConnected);

            var connectionUid = _client.ConnectionUid;
            Assert.True(_server.TryGetConnectionEndPoint(connectionUid, out var endPointAtConnect));

            // Same session, new source address - the client "moved". Its original socket goes quiet, exactly
            // as it would after a rebinding.
            using var movedClient = new UdpFullProtocol(port: 0, protocolKey: 0);
            var writer = new NetWriter();
            writer.SeekZero();
            writer.WriteByte((byte)ReliableUdpListener.ClientMessageCodes.Heartbeat);
            writer.WritePackedUInt32(connectionUid);
            writer.WritePackedUInt32(_client.ValidationUid);
            writer.WriteByte(FIRST_SLOT);
            var heartbeat = writer.AsArraySegment();

            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, _server.Port);
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < 400)
            {
                Thread.Sleep(millisecondsTimeout: 10);

                movedClient.SendTo(serverEndPoint, heartbeat, UdpFullProtocol.DgramDeliveryMethod.Unreliable);
                movedClient.Poll();
                _server.Update();
            }

            // Reported once the arrival address stopped matching.
            Assert.Contains(_capturedLogs, log => log.Contains("is now arriving from") && log.Contains($"'{connectionUid}'"));

            // But not followed: outgoing data still targets the address captured at connect.
            Assert.True(_server.TryGetConnectionEndPoint(connectionUid, out var endPointAfterMove));
            Assert.Equal(endPointAtConnect, endPointAfterMove);
        }

        /// <summary>
        /// The report must not fire for a session that has not moved, or it would log at the heartbeat rate
        /// on a process wide lock.
        /// </summary>
        [Fact]
        public void AStableSessionIsNeverReported()
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));
            PumpUntil(() => _client.IsConnected, ReliableUdpListener.HEARTBEAT_TIMEOUT_MS);
            Assert.True(_client.IsConnected);

            Pump(millisecondsToRun: 800);

            Assert.DoesNotContain(_capturedLogs, log => log.Contains("is now arriving from"));
        }

        private void PumpUntil(Func<bool> condition, uint millisecondsTimeout)
        {
            var startTime = DateTime.UtcNow;
            while (!condition() && (DateTime.UtcNow - startTime).TotalMilliseconds < millisecondsTimeout)
                Tick();
        }

        private void Pump(int millisecondsToRun)
        {
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < millisecondsToRun)
                Tick();
        }

        private void Tick()
        {
            Thread.Sleep(millisecondsTimeout: 10);

            _server.Update();
            _client.Update();
        }
    }
}