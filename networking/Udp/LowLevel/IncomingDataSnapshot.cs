using System.Net;

namespace Keel.Networking.Udp.LowLevel
{
    public readonly struct IncomingDataSnapshot
    {
        public readonly uint Uid;
        public readonly IPEndPoint EndPoint;
        public readonly byte[] Buffer;
        public readonly byte Channel;

        internal IncomingDataSnapshot(uint uid, IPEndPoint endPoint, byte[] data, byte channel)
        {
            Uid = uid;
            EndPoint = endPoint;
            Buffer = data;
            Channel = channel;
        }
    }
}