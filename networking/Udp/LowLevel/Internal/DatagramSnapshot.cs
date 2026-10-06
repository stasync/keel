using System;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Internal
{
    internal readonly struct DatagramSnapshot
    {
        internal readonly uint Uid;
        internal readonly IPEndPoint EndPoint;
        internal readonly ArraySegment<byte> Buffer;
        internal readonly byte Channel;

        internal DatagramSnapshot(uint uid, IPEndPoint endPoint, ArraySegment<byte> data, byte channel)
        {
            if (data.Count > MtuBuffer.SIZE)
                throw new InvalidOperationException();

            Uid = uid;
            EndPoint = endPoint;
            Buffer = MtuBuffer.Rent(data);
            Channel = channel;
        }

        internal void Dispose() =>
            MtuBuffer.Release(Buffer);
    }
}