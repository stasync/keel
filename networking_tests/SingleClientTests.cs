using Core.Networking.Udp.LowLevel;
using System.Net;

namespace Core.Networking.Tests
{
    public class SingleClientTests : IDisposable
    {
        private const ushort PROTOCOL_KEY = 23;

        private readonly int _serverPort;
        private readonly UdpFullProtocol _server;
        private readonly UdpFullProtocol _client;

        public SingleClientTests()
        {
            _serverPort = Utils.GetAvailableUdpPort();
            _server = new UdpFullProtocol(_serverPort, PROTOCOL_KEY);
            _client = new UdpFullProtocol(port: 0, PROTOCOL_KEY);
        }

        public void Dispose()
        {
            _server.Dispose();
            _client.Dispose();
        }

        [Fact]
        public void Test()
        {
            const string requestString = "Hello world form client";
            const string responsePattern = "SERVER: RESPOND ON ({0})";

            var localAddress = IPAddress.Loopback;

            // Queue message.
            {
                var writer = new NetWriter();
                writer.SeekZero();
                writer.WriteString(requestString);

                _client.SendTo(new IPEndPoint(localAddress, _serverPort), data: writer.AsArraySegment(), UdpFullProtocol.DgramDeliveryMethod.Reliable);
            }

            var receivedResponseFromServer = string.Empty;

            var startTime = DateTime.UtcNow;
            while (string.IsNullOrWhiteSpace(receivedResponseFromServer) && (DateTime.UtcNow - startTime).TotalMilliseconds < 2000)
            {
                Thread.Sleep(millisecondsTimeout: 10);

                _server.Poll();
                _client.Poll();

                // Server
                {
                    while (_server.TryDequeueIncoming(out var incomingData))
                    {
                        var reader = new NetReader(incomingData.Buffer);
                        var request = reader.ReadString();

                        var respondWriter = new NetWriter();
                        respondWriter.WriteString(string.Format(responsePattern, request));

                        _server.SendTo(incomingData.EndPoint, data: respondWriter.AsArraySegment(), (UdpFullProtocol.DgramDeliveryMethod)incomingData.ProtocolPrefix);
                    }
                }

                // Client
                {
                    while (_client.TryDequeueIncoming(out var incomingData))
                    {
                        var reader = new NetReader(incomingData.Buffer);
                        receivedResponseFromServer = reader.ReadString();
                    }
                }
            }

            // Test the result.
            Assert.Equal(string.Format(responsePattern, requestString), receivedResponseFromServer);
        }
    }
}