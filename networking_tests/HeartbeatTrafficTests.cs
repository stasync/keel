using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel.DataTransferLayers;
using Keel.Networking.Udp.LowLevel.Simulators;
using System.Net;
using Xunit.Abstractions;

namespace Keel.Networking.Tests
{
    /// <summary>
    /// Heartbeats are periodic, so they must not be sent reliably.
    ///
    /// ProtocolState.Update() resends every unacked datagram on every Poll tick with no backoff, so a reliable
    /// heartbeat is retransmitted until it is acked - and every copy is acked in turn. That makes the cost of
    /// carrying a fixed 10 heartbeats/sec scale with latency, which is the opposite of what a keepalive should
    /// do. A lost unreliable heartbeat needs no recovery: the next one follows in HEARTBEAT_SEND_TIMEOUT_MS,
    /// and HEARTBEAT_TIMEOUT_MS tolerates roughly 20 consecutive losses.
    ///
    /// The two tests are a pair - the first is only worth having if the second holds.
    /// </summary>
    public class HeartbeatTrafficTests : IDisposable
    {
        private const int SETTLE_MS = 500;
        private const int STEADY_STATE_MS = 1500;

        /// <summary>
        /// A heartbeat should cost exactly one datagram, with a little slack for timing jitter at the window
        /// edges. Reliable heartbeats measure an order of magnitude above this at any real latency.
        /// </summary>
        private const double MAX_DATAGRAMS_PER_HEARTBEAT = 1.5;

        private readonly ITestOutputHelper _output;

        private readonly ReliableUdpListener _server;
        private readonly ReliableUdpClient _client = new();
        private readonly DataTransferAmountCaptureLayer _serverCapture = new();
        private readonly DataTransferAmountCaptureLayer _clientCapture = new();

        private int _heartbeatsSent;

        public HeartbeatTrafficTests(ITestOutputHelper output)
        {
            _output = output;

            _server = new ReliableUdpListener(maxConnections: 4, port: 0, protocolKey: 0);
            _server.RegisterDataTransferLayer(_serverCapture);

            _client.HeartbeatSent += () => _heartbeatsSent++;
        }

        public void Dispose()
        {
            _server.Dispose();
            _client.Disconnect();
        }

        [Fact]
        public void SteadyStateHeartbeatsCostOneDatagramEach()
        {
            const int oneWayDelayMs = 30;

            _server.RegisterIncomingPacketSimulator(FixedDelay(oneWayDelayMs));
            ConnectClient(client => client.RegisterIncomingPacketSimulator(FixedDelay(oneWayDelayMs)));

            // Let the reliable connect handshake finish retransmitting before opening the window. The handshake
            // is reliable by design and is not what is at issue here.
            Pump(SETTLE_MS);

            var clientPacketsAtStart = _clientCapture.PacketsSent;
            var serverPacketsAtStart = _serverCapture.PacketsSent;
            var heartbeatsAtStart = _heartbeatsSent;

            Pump(STEADY_STATE_MS);

            var heartbeats = _heartbeatsSent - heartbeatsAtStart;
            var clientPackets = _clientCapture.PacketsSent - clientPacketsAtStart;
            var serverPackets = _serverCapture.PacketsSent - serverPacketsAtStart;

            Assert.True(heartbeats > 0, "No heartbeats were sent during the measurement window.");
            Assert.True(_client.IsConnected, "The connection did not survive the measurement window.");

            var clientRatio = clientPackets / (double)heartbeats;
            var serverRatio = serverPackets / (double)heartbeats;

            _output.WriteLine($"round trip           : {oneWayDelayMs * 2}ms");
            _output.WriteLine($"logical heartbeats   : {heartbeats}");
            _output.WriteLine($"client datagrams sent: {clientPackets}  ({clientRatio:F1}x per heartbeat)");
            _output.WriteLine($"server datagrams sent: {serverPackets}  ({serverRatio:F1}x per heartbeat)");

            Assert.True(clientRatio <= MAX_DATAGRAMS_PER_HEARTBEAT,
                $"Client sent {clientRatio:F1} datagrams per heartbeat (limit {MAX_DATAGRAMS_PER_HEARTBEAT:F1}).");
            Assert.True(serverRatio <= MAX_DATAGRAMS_PER_HEARTBEAT,
                $"Server sent {serverRatio:F1} datagrams per heartbeat (limit {MAX_DATAGRAMS_PER_HEARTBEAT:F1}).");
        }

        /// <summary>
        /// The counterpart to the traffic measurement: dropping reliability must not cost stability. A lost
        /// heartbeat is only survivable because the next one is already on its way.
        /// </summary>
        [Fact]
        public void ConnectionSurvivesLossAndLatencyWithoutReliableHeartbeats()
        {
            const int oneWayDelayMs = 100;
            const float lossPercent = 20f;

            _server.RegisterIncomingPacketSimulator(Loss(lossPercent));
            _server.RegisterIncomingPacketSimulator(FixedDelay(oneWayDelayMs));

            ConnectClient(client =>
            {
                client.RegisterIncomingPacketSimulator(Loss(lossPercent));
                client.RegisterIncomingPacketSimulator(FixedDelay(oneWayDelayMs));
            });

            var connectionUid = _client.ConnectionUid;
            var disconnected = false;
            _client.Disconnected += () => disconnected = true;

            // Longer than HEARTBEAT_TIMEOUT_MS, so a connection that cannot hold up would drop inside it.
            Pump(millisecondsToRun: 3000);

            _output.WriteLine($"round trip: {oneWayDelayMs * 2}ms, loss: {lossPercent}% each way");
            _output.WriteLine($"heartbeats sent: {_heartbeatsSent}");

            Assert.False(disconnected, "Client reported a disconnect under loss.");
            Assert.True(_client.IsConnected, "Client dropped the connection under loss.");
            Assert.True(_server.HasConnection(connectionUid), "Server timed the connection out under loss.");
        }

        private void ConnectClient(Action<ReliableUdpClient> configure)
        {
            _client.Connect(new IPEndPoint(IPAddress.Loopback, _server.Port));

            // Registered after Connect, which is what creates the underlying protocol.
            _client.RegisterDataTransferLayer(_clientCapture);
            configure(_client);

            PumpUntil(() => _client.IsConnected, ReliableUdpListener.HEARTBEAT_TIMEOUT_MS);
            Assert.True(_client.IsConnected, "Client failed to connect.");
        }

        private static SimulatePacketDelay FixedDelay(int oneWayDelayMs) =>
            new()
            {
                PacketMaxDelayMs = oneWayDelayMs,
                PacketMinDelayMs = oneWayDelayMs
            };

        private static SimulatePacketLossPercentage Loss(float lossPercent) =>
            new() { PacketLossPercent = lossPercent };

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