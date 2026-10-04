using System.Net;
using System.Net.Sockets;

namespace Keel.Networking.Tests
{
    public class SamePortListeningTests
    {
        [Fact]
        public void TcpAndUdp_CanListenOnSamePort()
        {
            int port = GetAvailablePort();
            TcpListener tcpListener = null;
            UdpClient udpListener = null;

            try
            {
                tcpListener = new TcpListener(localaddr: IPAddress.Loopback, port);
                tcpListener.Start();

                udpListener = new UdpClient(port);

                Assert.True(tcpListener.Server.IsBound);
                Assert.True(udpListener.Client.IsBound);
            }
            finally
            {
                tcpListener?.Stop();
                udpListener?.Close();
            }
        }

        [Fact]
        public void TcpAndUdp_BothReportSamePort()
        {
            var port = GetAvailablePort();
            TcpListener tcpListener = null;
            UdpClient udpListener = null;

            try
            {
                tcpListener = new TcpListener(localaddr: IPAddress.Loopback, port);
                tcpListener.Start();
                udpListener = new UdpClient(port);

                var tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                var udpPort = ((IPEndPoint)udpListener.Client.LocalEndPoint)!.Port;

                Assert.Equal(port, tcpPort);
                Assert.Equal(port, udpPort);
            }
            finally
            {
                tcpListener?.Stop();
                udpListener?.Close();
            }
        }

        private static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port: 0));
            return ((IPEndPoint)socket.LocalEndPoint)!.Port;
        }
    }
}