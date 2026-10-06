using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Keel.Networking.Udp.LowLevel.DataTransferLayers
{
    public sealed class DataTransferAmountCaptureLayer : DataTransferLayer
    {
        public int BytesSent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int BytesReceived
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int PacketsSent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int PacketsReceived
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data)
        {
            BytesSent += data.Count;
            PacketsSent++;
        }

        public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data)
        {
            BytesReceived += data.Count;
            PacketsReceived++;
        }
    }
}