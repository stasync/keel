using System.Net;

namespace Core.Networking.Udp.LowLevel
{
    public readonly struct IncomingDataSnapshot
    {
        public readonly uint Uid;
        public readonly IPEndPoint EndPoint;
        public readonly byte[] Buffer;
        public readonly byte ProtocolPrefix;

        internal IncomingDataSnapshot(uint uid, IPEndPoint endPoint, byte[] data, byte protocolPrefix)
        {
            Uid = uid;
            EndPoint = endPoint;
            Buffer = data;
            ProtocolPrefix = protocolPrefix;
        }
    }
}