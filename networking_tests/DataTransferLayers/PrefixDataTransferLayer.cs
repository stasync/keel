using Keel.Networking.Udp.LowLevel;
using System.Net;

namespace Keel.Networking.Tests.DataTransferLayers
{
    /// <summary>
    /// A data transfer layer that adds prefix in front of the message.
    /// </summary>
    internal sealed class PrefixDataTransferLayer : DataTransferLayer
    {
        private const byte PREFIX = 69;

        public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data)
        {
            // Add a single prefix here.
            var newData = new byte[data.Count + 1];
            Buffer.BlockCopy(src: data.Array!, srcOffset: 0, dst: newData, dstOffset: 1, count: data.Count);
            newData[0] = PREFIX;

            // Update data.
            data = new ArraySegment<byte>(newData);
        }

        public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data)
        {
            // Validate prefix.
            var prefix = data[0];
            Assert.Equal(PREFIX, prefix);

            // Remove prefix from the message.
            var newData = new byte[data.Count - 1];
            Buffer.BlockCopy(src: data.Array!, srcOffset: 1, dst: newData, dstOffset: 0, count: data.Count - 1);

            // Update data.
            data = new ArraySegment<byte>(newData);
        }
    }
}