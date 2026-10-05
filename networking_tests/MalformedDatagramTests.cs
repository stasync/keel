using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel;
using System.Net;

namespace Keel.Networking.Tests
{
    /// <summary>
    /// A malformed datagram must never escape <see cref="ReliableUdpClient.Update"/>.
    /// Without the guard in ProcessData an unknown server message code throws out of Update(),
    /// so the client stops heartbeat, never runs its own timeout check, logs nothing,
    /// and the server drops it once <see cref="ReliableUdpServer.HEARTBEAT_TIMEOUT_MS"/> elapses.
    ///
    /// Uses a raw <see cref="UdpProtocol"/> as the server so the test can put arbitrary
    /// bytes on the wire - <see cref="ReliableUdpServer"/> only ever sends well-formed messages.
    /// </summary>
    public class MalformedDatagramTests : IDisposable
    {
        private const byte UNKNOWN_SERVER_MESSAGE_CODE = 99;
        private const uint CONNECTION_UID = 7;
        private const uint VALIDATION_UID = 123456;
        private const byte CONNECTION_SLOT = 0;

        private readonly int _serverPort;
        private readonly UdpProtocol _fakeServer;
        private readonly ReliableUdpClient _client = new();
        private readonly NetWriter _writer = new();
        private readonly NetReader _reader = new();

        private IPEndPoint _clientEndPoint;

        public MalformedDatagramTests()
        {
            _serverPort = Utils.GetAvailableUdpPort();

            // The client always connects with protocol key 0, so the fake server has to match.
            _fakeServer = new UdpProtocol(_serverPort, protocolKey: 0);
        }

        public void Dispose()
        {
            _fakeServer.Dispose();
            _client.Disconnect();
        }

        [Fact]
        public void UnknownServerMessageCodeIsDiscardedAndTheUpdateLoopSurvives()
        {
            var heartbeatsSent = 0;
            _client.HeartbeatSent += () => heartbeatsSent++;

            _client.Connect(new IPEndPoint(IPAddress.Loopback, _serverPort));

            PumpUntilConnected();
            Assert.True(_client.IsConnected);
            Assert.NotNull(_clientEndPoint);

            var heartbeatsBeforeInjection = heartbeatsSent;

            // Inject one unparsable datagram. Reliable so that it is guaranteed to arrive.
            _writer.SeekZero();
            _writer.WriteByte(UNKNOWN_SERVER_MESSAGE_CODE);
            _fakeServer.SendTo(_clientEndPoint, _writer.AsArraySegment(), UdpProtocol.DeliveryMethod.Reliable);

            // Keep the session running well past the point the bad datagram was delivered.
            Pump(millisecondsToRun: 1000);

            // The bad datagram was discarded and the loop kept running.
            Assert.True(_client.IsConnected);
            Assert.True(heartbeatsSent > heartbeatsBeforeInjection,
                $"Client stopped heart beating after the malformed datagram: {heartbeatsBeforeInjection} -> {heartbeatsSent}.");
        }

        private void PumpUntilConnected()
        {
            var startTime = DateTime.UtcNow;
            while (!_client.IsConnected && (DateTime.UtcNow - startTime).TotalMilliseconds < ReliableUdpServer.HEARTBEAT_TIMEOUT_MS)
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

            PumpFakeServer();
            _client.Update();
        }

        /// <summary>
        /// Minimal server side of the handshake: confirm the connection and answer heartbeats,
        /// so the client stays up for the duration of the test.
        /// </summary>
        private void PumpFakeServer()
        {
            _fakeServer.Poll();

            while (_fakeServer.TryDequeueIncoming(out var incomingData))
            {
                _clientEndPoint = incomingData.EndPoint;

                _reader.Replace(incomingData.Buffer);
                var messageCode = (ReliableUdpServer.ClientMessageCodes)_reader.ReadByte();

                _writer.SeekZero();
                switch (messageCode)
                {
                    case ReliableUdpServer.ClientMessageCodes.Connect:
                        _writer.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.ConnectConfirmed);
                        _writer.WritePackedUInt32(CONNECTION_UID);
                        _writer.WritePackedUInt32(VALIDATION_UID);
                        _writer.WriteByte(CONNECTION_SLOT);
                        _fakeServer.SendTo(incomingData.EndPoint, _writer.AsArraySegment(), UdpProtocol.DeliveryMethod.Reliable);
                        break;

                    case ReliableUdpServer.ClientMessageCodes.Heartbeat:
                        _writer.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.Heartbeat);
                        _fakeServer.SendTo(incomingData.EndPoint, _writer.AsArraySegment(), UdpProtocol.DeliveryMethod.Unreliable);
                        break;
                }
            }
        }
    }
}