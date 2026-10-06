using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel;
using Keel.Networking.Udp.LowLevel.Simulators;
using System.Net;

namespace Keel.Networking.Tests
{
    /// <summary>
    /// Covers how <see cref="ReliableUdpServer"/> classifies a client message whose connection slot
    /// does not resolve to a live session. Callers may ban the address on a single
    /// <see cref="ReliableUdpServer.UnexpectedClientAction"/>, so the distinction matters.
    /// </summary>
    public class ConnectionSlotTests : IDisposable
    {
        private const int MAX_CONNECTIONS = 4;

        private readonly ReliableUdpServer _server;
        private readonly ReliableUdpClient _client = new();
        private readonly List<string> _unexpectedClientActions = new();

        public ConnectionSlotTests()
        {
            _server = new ReliableUdpServer(MAX_CONNECTIONS, port: 0, protocolKey: 0);
            _server.UnexpectedClientAction += (_, action) =>
                _unexpectedClientActions.Add(action);
        }

        public void Dispose()
        {
            _server.Dispose();
            _client.Disconnect();
        }

        /// <summary>
        /// Server initiated kick. Disconnect() nulls the slot immediately, but the client keeps heartbeating
        /// at 10/s until the Disconnect message reaches it - one RTT of heartbeats hitting a dead slot.
        /// </summary>
        [Fact]
        public void HeartbeatsArrivingAfterAKickAreNotReportedAsUnexpected()
        {
            ConnectWithDelayedIncoming();

            var connectionUid = _client.ConnectionUid;
            _server.Disconnect(connectionUid);
            Assert.False(_server.HasConnection(connectionUid));

            Pump(millisecondsToRun: 800);

            Assert.Empty(_unexpectedClientActions);
        }

        /// <summary>
        /// The same race reached through the heartbeat timeout path rather than an explicit kick.
        /// </summary>
        [Fact]
        public void HeartbeatsArrivingAfterATimeoutAreNotReportedAsUnexpected()
        {
            ConnectWithDelayedIncoming();

            // Starve the server of heartbeats so it times the connection out on its own, while the client
            // stays alive and keeps sending.
            var connectionUid = _client.ConnectionUid;
            _server.RegisterIncomingPacketSimulator(new DropAllPacketsSimulator());

            PumpUntil(() => !_server.HasConnection(connectionUid), ReliableUdpServer.HEARTBEAT_TIMEOUT_MS * 2);
            Assert.False(_server.HasConnection(connectionUid));

            // Let the client's heartbeats reach the server again, now that the slot is gone.
            _server.UnregisterAllIncomingPacketSimulators();
            Pump(millisecondsToRun: 500);

            Assert.Empty(_unexpectedClientActions);
        }

        private void ConnectWithDelayedIncoming()
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));

            // Delay only what the client receives, so the server side teardown is held for a known interval while
            // the client keeps heartbeat. Registered after Connect, which creates the protocol, but before
            // the first pump, so nothing has been received yet.
            _client.RegisterIncomingPacketSimulator(new PacketDelaySimulator
            {
                PacketMaxDelayMs = 300,
                PacketMinDelayMs = 300
            });

            PumpUntil(() => _client.IsConnected, ReliableUdpServer.HEARTBEAT_TIMEOUT_MS);

            Assert.True(_client.IsConnected);
            Assert.Empty(_unexpectedClientActions);
        }

        private void PumpUntil(Func<bool> condition, uint millisecondsTimeout, UdpProtocol alsoPoll = null)
        {
            var startTime = DateTime.UtcNow;
            while (!condition() && (DateTime.UtcNow - startTime).TotalMilliseconds < millisecondsTimeout)
                Tick(alsoPoll);
        }

        private void Pump(int millisecondsToRun)
        {
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < millisecondsToRun)
                Tick(alsoPoll: null);
        }

        private void Tick(UdpProtocol alsoPoll)
        {
            Thread.Sleep(millisecondsTimeout: 10);

            _server.Update();
            _client.Update();
            alsoPoll?.Poll();
        }
    }
}