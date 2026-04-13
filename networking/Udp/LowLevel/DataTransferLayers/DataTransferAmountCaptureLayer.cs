using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel.DataTransferLayers
{
    public sealed class DataTransferAmountCaptureLayer : UdpReliableProtocol.DataTransferLayer
    {
        public int Sent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int Received
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data) =>
            Sent += data.Count;

        public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data) =>
            Received += data.Count;
    }
}