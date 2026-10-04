using Keel.Networking.Udp.LowLevel;
using System.Net;

namespace Keel.Networking.Tests.DataTransferLayers
{
    /// <summary>
    /// A data transfer layer that adds prefix in font of the message.
    /// </summary>
    internal sealed class PostfixDataTransferLayer : UdpReliableProtocol.DataTransferLayer
    {
        private const byte POSTFIX = 69;

        public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data)
        {
            // Add a single postfix here.
            var newData = new byte[data.Count + 1];
            Buffer.BlockCopy(src: data.Array!, srcOffset: 0, dst: newData, dstOffset: 0, count: data.Count);
            newData[^1] = POSTFIX;

            // Update data.
            data = new ArraySegment<byte>(newData);
        }

        public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data)
        {
            // Validate postfix.
            var postfix = data[^1];
            Assert.Equal(POSTFIX, postfix);

            // Remove prefix from the message.
            var newData = new byte[data.Count - 1];
            Buffer.BlockCopy(src: data.Array!, srcOffset: 0, dst: newData, dstOffset: 0, count: data.Count - 1);

            // Update data.
            data = new ArraySegment<byte>(newData);
        }
    }
}