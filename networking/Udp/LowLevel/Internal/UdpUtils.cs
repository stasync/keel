using Core.Utils.Debug;
using System;
using System.Net;
using System.Net.Sockets;

namespace Core.Networking.Udp.LowLevel.Internal
{
    internal static class UdpUtils
    {
        public static Socket CreateUdpSocket() =>
            new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                Blocking = false,
                DontFragment = true
            };

        internal static void SendMtu(Socket socket, IPEndPoint endPoint, ArraySegment<byte> data)
        {
            try
            {
                if (data.Array == null)
                    throw new NullReferenceException();

                if (data.Count > MtuBuffer.SIZE)
                    throw new InvalidOperationException();

                socket.SendTo(data.Array, data.Offset, data.Count, SocketFlags.None, endPoint);
            }
            catch (SocketException)
            {
                // ignored
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        internal static int ReceiveMtuFrom(Socket socket, byte[] buffer, ref EndPoint endPoint)
        {
            try
            {
                if (buffer == null)
                    throw new NullReferenceException();

                if (buffer.Length > MtuBuffer.SIZE)
                    throw new InvalidOperationException();

                return socket.ReceiveFrom(buffer, SocketFlags.None, ref endPoint);
            }
            catch (SocketException)
            {
                // ignored
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return 0;
        }

        public static void CloseSocket(Socket socket, bool throwException = false)
        {
            try
            {
                socket.Shutdown(how: SocketShutdown.Both);
            }
            catch
            {
                if (throwException)
                    throw;
            }
            finally
            {
                socket.Close();
            }
        }
    }
}