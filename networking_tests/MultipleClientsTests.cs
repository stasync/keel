using Keel.Networking.Tests.DataTransferLayers;
using Keel.Networking.Udp.LowLevel;
using Keel.Networking.Udp.LowLevel.DataTransferLayers;
using Keel.Networking.Udp.LowLevel.Simulators;
using System.Net;

namespace Keel.Networking.Tests
{
    public class MultipleClientsTests : IDisposable
    {
        private const ushort PROTOCOL_KEY = 23;

        private readonly int _serverPort;
        private readonly UdpProtocol _server;
        private readonly UdpProtocol[] _clients = new UdpProtocol[8];

        public MultipleClientsTests()
        {
            const string encryptionKey = "c7fdcdc0-2489-4a5d-8932-e5e819e1add7";
            _serverPort = Utils.GetAvailableUdpPort();
            _server = new UdpProtocol(_serverPort, protocolKey: PROTOCOL_KEY);

            _server.RegisterDataTransferLayer(new PrefixDataTransferLayer());
            _server.RegisterDataTransferLayer(new PostfixDataTransferLayer());
            _server.RegisterDataTransferLayer(new DataTransferXorObfuscationLayer(encryptionKey));

            _server.RegisterIncomingPacketSimulator(new PeriodicPacketLossSimulator
            {
                PacketLossPercent = 25f
            });
            _server.RegisterIncomingPacketSimulator(new RandomPacketDuplicationSimulator());
            _server.RegisterIncomingPacketSimulator(new PacketDelaySimulator
            {
                PacketMinDelayMs = 30f,
                PacketMaxDelayMs = 150f,
                ReverseDelayedPackets = true
            });

            for (var i = 0; i < _clients.Length; i++)
            {
                _clients[i] = new UdpProtocol(port: 0, PROTOCOL_KEY);

                // Should be reversed order on the client.
                _clients[i].RegisterDataTransferLayer(new PrefixDataTransferLayer());
                _clients[i].RegisterDataTransferLayer(new PostfixDataTransferLayer());
                _clients[i].RegisterDataTransferLayer(new DataTransferXorObfuscationLayer(encryptionKey));

                _clients[i].RegisterIncomingPacketSimulator(new PeriodicPacketLossSimulator
                {
                    PacketLossPercent = 25f
                });
                _clients[i].RegisterIncomingPacketSimulator(new PacketDelaySimulator
                {
                    PacketMinDelayMs = 30f,
                    PacketMaxDelayMs = 150f,
                    ReverseDelayedPackets = true
                });
            }
        }

        public void Dispose()
        {
            _server.Dispose();
            foreach (var client in _clients)
                client.Dispose();
        }

        [Fact]
        public void Test()
        {
            const string requestStringPattern = "Hello world form client ({0})";
            const string responsePattern = "SERVER: RESPOND ON ({0})";

            var localAddress = IPAddress.Loopback;

            for (var i = 0; i < _clients.Length; i++)
            {
                var writer = new NetWriter();
                writer.SeekZero();
                writer.WriteString(string.Format(requestStringPattern, i));
                // Switch too unreliable to fail the test, due to assigned simulators.
                _clients[i].SendTo(new IPEndPoint(localAddress, _serverPort), data: writer.AsArraySegment(), UdpProtocol.DeliveryMethod.ReliableOrdered);
            }

            var receivedResponseFromServer = new string[_clients.Length];

            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < 2000)
            {
                Thread.Sleep(millisecondsTimeout: 10);
                _server.Poll();

                for (var i = 0; i < _clients.Length; i++)
                {
                    _clients[i].Poll();

                    while (_clients[i].TryDequeueIncoming(out var incomingData))
                    {
                        var reader = new NetReader(incomingData.Buffer);
                        receivedResponseFromServer[i] = reader.ReadString();
                    }
                }

                // Server
                {
                    while (_server.TryDequeueIncoming(out var incomingData))
                    {
                        var reader = new NetReader(incomingData.Buffer);
                        var request = reader.ReadString();

                        var respondWriter = new NetWriter();
                        respondWriter.WriteString(string.Format(responsePattern, request));

                        _server.SendTo(incomingData.EndPoint, data: respondWriter.AsArraySegment(), (UdpProtocol.DeliveryMethod)incomingData.Channel);
                    }
                }

                var allExpectedDataReceived = true;
                foreach (var response in receivedResponseFromServer)
                {
                    if (!string.IsNullOrWhiteSpace(response))
                        continue;

                    allExpectedDataReceived = false;
                    break;
                }

                if (allExpectedDataReceived)
                    break;
            }

            Assert.Equal(0, _server.ReliabilityFailureCount);
            foreach (var client in _clients)
                Assert.Equal(0, client.ReliabilityFailureCount);

            // Test the result.
            for (var i = 0; i < receivedResponseFromServer.Length; i++)
            {
                Assert.True(!string.IsNullOrWhiteSpace(receivedResponseFromServer[i]));

                var request = string.Format(requestStringPattern, i);
                Assert.Equal(string.Format(responsePattern, request), receivedResponseFromServer[i]);
            }
        }
    }
}