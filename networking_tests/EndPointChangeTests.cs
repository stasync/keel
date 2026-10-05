using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel;
using System.Net;

namespace Keel.Networking.Tests
{
    /// <summary>
    /// A session can start arriving from a different endpoint - a NAT rebind, a Wi-Fi handover, a changed
    /// public address. Heartbeats would keep working either way, because the reply goes back to whatever
    /// address the datagram arrived from, so the session would look healthy. But outgoing data keeps targeting
    /// the address captured at connect, so the client would silently stop receiving while still appearing
    /// connected.
    ///
    /// The endpoint is deliberately not followed - doing so would let anyone knowing the connection uid,
    /// validation uid and slot redirect a session to their own address. Instead the datagram is reported
    /// through <see cref="ReliableUdpServer.UnexpectedClientAction"/> and discarded.
    /// </summary>
    public class EndPointChangeTests : IDisposable
    {
        /// <summary>A single client on a fresh server always lands in the first slot.</summary>
        private const byte FIRST_SLOT = 0;

        private readonly ReliableUdpServer _server;
        private readonly ReliableUdpClient _client = new();
        private readonly List<string> _unexpectedClientActions = new();

        public EndPointChangeTests()
        {
            _server = new ReliableUdpServer(maxConnections: 4, port: 0, protocolKey: 0);
            _server.UnexpectedClientAction += (_, action) =>
                _unexpectedClientActions.Add(action);
        }

        public void Dispose()
        {
            _server.Dispose();
            _client.Disconnect();
        }

        [Fact]
        public void SessionArrivingFromANewEndpointIsReportedButNotFollowed()
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));
            PumpUntil(() => _client.IsConnected, ReliableUdpServer.HEARTBEAT_TIMEOUT_MS);
            Assert.True(_client.IsConnected);

            var connectionUid = _client.ConnectionUid;
            Assert.True(_server.TryGetConnectionEndPoint(connectionUid, out var endPointAtConnect));
            Assert.Empty(_unexpectedClientActions);

            // Same session, new source address - the client "moved". Its original socket goes quiet, exactly
            // as it would after a rebinding.
            using var movedClient = new UdpProtocol(port: 0, protocolKey: 0);
            var writer = new NetWriter();
            writer.SeekZero();
            writer.WriteByte((byte)ReliableUdpServer.ClientMessageCodes.Heartbeat);
            writer.WritePackedUInt32(connectionUid);
            writer.WritePackedUInt32(_client.ValidationUid);
            writer.WriteByte(FIRST_SLOT);
            var heartbeat = writer.AsArraySegment();

            var serverEndPoint = new IPEndPoint(IPAddress.Loopback, _server.Port);
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < 400)
            {
                Thread.Sleep(millisecondsTimeout: 10);

                movedClient.SendTo(serverEndPoint, heartbeat, UdpProtocol.DeliveryMethod.Unreliable);
                movedClient.Poll();
                _server.Update();
            }

            // Reported once the arrival address stopped matching the pinned one.
            Assert.Contains(_unexpectedClientActions,
                action => action.Contains("is now arriving from") && action.Contains($"'{connectionUid}'"));

            // But not followed: outgoing data still targets the address captured at connect.
            Assert.True(_server.TryGetConnectionEndPoint(connectionUid, out var endPointAfterMove));
            Assert.Equal(endPointAtConnect, endPointAfterMove);
        }

        /// <summary>
        /// A session that has not moved must never be reported - callers may ban an address on a single
        /// <see cref="ReliableUdpServer.UnexpectedClientAction"/>, so a false positive here would drop a
        /// healthy client.
        /// </summary>
        [Fact]
        public void AStableSessionIsNeverReported()
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));
            PumpUntil(() => _client.IsConnected, ReliableUdpServer.HEARTBEAT_TIMEOUT_MS);
            Assert.True(_client.IsConnected);

            Pump(millisecondsToRun: 800);

            Assert.Empty(_unexpectedClientActions);
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