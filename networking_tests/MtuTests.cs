using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel;
using System.Net;

namespace Keel.Networking.Tests
{
    public class MtuTests : IDisposable
    {
        private const uint TIMEOUT = ReliableUdpServer.HEARTBEAT_TIMEOUT_MS + 200;

        private readonly ReliableUdpServer _server = new(maxConnections: 16, port: 0, protocolKey: 0);
        private readonly ReliableUdpClient _client = new();

        public void Dispose()
        {
            _server.Dispose();
            _client.Disconnect();
        }

        [Fact]
        public void Test()
        {
            // Connect.
            {
                /*_server.DataReceived += (connUid, data, deliverMethod) =>
                {
                };*/

                var localAddress = IPAddress.Loopback;
                _client.Connect(new IPEndPoint(localAddress, _server.Port), connectionData: string.Empty);

                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < TIMEOUT && !_client.IsConnected)
                {
                    Thread.Sleep(millisecondsTimeout: 10);

                    _server.Update();
                    _client.Update();
                }

                Assert.True(_server.HasConnection(_client.ConnectionUid));
                Assert.True(_client.IsConnected);

                // Large data blob.
                var nonMtu = new byte[1452 * 3];

                // Should throw.
                Assert.ThrowsAny<Exception>(() =>
                    _client.Send(nonMtu, UdpFullProtocol.DgramDeliveryMethod.Reliable));
            }
        }
    }
}