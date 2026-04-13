using Core.Networking.Udp;
using System.Net;

namespace Core.Networking.Tests
{
    /// <summary>
    /// Test for high level UPD api - <see cref="ReliableUdpListener"/> & <see cref="ReliableUdpClient"/>.
    /// </summary>
    public class HighLevelUpdTests : IDisposable
    {
        private const uint TIMEOUT = ReliableUdpListener.HEARTBEAT_TIMEOUT_MS + 200;

        private readonly ReliableUdpListener _server;
        private readonly ReliableUdpClient[] _clients = new ReliableUdpClient[8];

        public HighLevelUpdTests()
        {
            _server = new ReliableUdpListener(maxConnections: 16, port: 0, protocolKey: 0);

            for (var i = 0; i < _clients.Length; i++)
                _clients[i] = new ReliableUdpClient();
        }

        public void Dispose()
        {
            _server.Dispose();
            foreach (var client in _clients)
                client.Disconnect();
        }

        /// <summary>
        /// TODO: Brake down to multiple tests - probably need to implement test context class, which will start up all the relevant services.
        /// </summary>
        [Fact]
        public void Test()
        {
            Assert.NotEqual(0, _server.Port);

            var trackedServerConnections = new HashSet<uint>();
            var trackedClientConnections = new HashSet<uint>();
            // Connect.
            {
                _server.Connected += (connectionUid, _) =>
                {
                    trackedServerConnections.Add(connectionUid);
                };

                _server.Disconnected += connectionUid =>
                {
                    trackedServerConnections.Remove(connectionUid);
                };

                var localAddress = IPAddress.Loopback;
                foreach (var client in _clients)
                {
                    client.Connected += () =>
                    {
                        var connectionUid = client.ConnectionUid;
                        trackedClientConnections.Add(connectionUid);

                        client.Disconnected += () =>
                        {
                            trackedClientConnections.Remove(connectionUid);
                        };
                    };

                    client.Connect(new IPEndPoint(localAddress, _server.Port));
                }

                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < TIMEOUT && trackedServerConnections.Count != _clients.Length)
                {
                    Thread.Sleep(millisecondsTimeout: 10);

                    _server.Update();
                    foreach (var client in _clients)
                        client.Update();
                }

                Assert.Equal(_clients.Length, trackedServerConnections.Count);
                Assert.Equal(_clients.Length, trackedClientConnections.Count);

                foreach (var connectionUid in trackedServerConnections)
                    Assert.True(_server.HasConnection(connectionUid));
            }

            // Disconnect
            {
                // Drop last client
                var clientToDrop = _clients[^1];
                var clientToDropConnectionId = clientToDrop.ConnectionUid;

                clientToDrop.Disconnect();
                Assert.False(clientToDrop.IsConnected);

                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < TIMEOUT && _server.HasConnection(clientToDropConnectionId))
                {
                    Thread.Sleep(millisecondsTimeout: 10);

                    _server.Update();
                    foreach (var client in _clients)
                        client?.Update();
                }

                // One client was removed.
                Assert.Equal(_clients.Length - 1, trackedServerConnections.Count);
                Assert.Equal(_clients.Length - 1, trackedClientConnections.Count);

                foreach (var connectionUid in trackedServerConnections)
                    Assert.True(_server.HasConnection(connectionUid));

                Assert.False(clientToDrop.IsConnected);
                Assert.False(_server.HasConnection(clientToDropConnectionId));
            }

            // Kick
            {
                // Drop last client
                var clientToDrop = _clients[^2];
                var clientToDropConnectionId = clientToDrop.ConnectionUid;

                _server.Disconnect(clientToDropConnectionId);
                Assert.False(_server.HasConnection(clientToDropConnectionId));

                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < TIMEOUT && clientToDrop.IsConnected)
                {
                    Thread.Sleep(millisecondsTimeout: 10);

                    _server.Update();
                    foreach (var client in _clients)
                        client?.Update();
                }

                // Two clients was removed.
                Assert.Equal(_clients.Length - 2, trackedServerConnections.Count);
                Assert.Equal(_clients.Length - 2, trackedClientConnections.Count);

                foreach (var connectionUid in trackedServerConnections)
                    Assert.True(_server.HasConnection(connectionUid));

                Assert.False(clientToDrop.IsConnected);
                Assert.False(_server.HasConnection(clientToDropConnectionId));
            }

            // Server close.
            {
                // Drop last client
                _server.Dispose();
                trackedServerConnections.Clear();

                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < TIMEOUT && trackedClientConnections.Count > 0)
                {
                    Thread.Sleep(millisecondsTimeout: 10);
                    foreach (var client in _clients)
                        client?.Update();
                }

                // Two clients was removed.
                Assert.Empty(trackedClientConnections);
                foreach (var client in _clients)
                    Assert.False(client.IsConnected);
            }
        }
    }
}