using System;
using System.Net;

namespace Keel.Networking.Udp.LowLevel
{
    /// <summary>
    /// Abstract class to implement a custom layer to process incoming/outgoing data.
    /// </summary>
    public abstract class DataTransferLayer
    {
        public abstract void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data);
        public abstract void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data);
    }
}