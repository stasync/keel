using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Keel.Networking.Udp.LowLevel.DataTransferLayers
{
    public sealed class DataTransferXorObfuscationLayer : UdpReliableProtocol.DataTransferLayer
    {
        private readonly byte[] _keyBytes;

        public DataTransferXorObfuscationLayer(string key = "") =>
            _keyBytes = string.IsNullOrWhiteSpace(key) ? null : System.Text.Encoding.UTF8.GetBytes(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EncryptDecryptXor(ArraySegment<byte> data)
        {
            if (_keyBytes == null)
                return;

            for (var i = 0; i < data.Count; i++)
                data.Array![data.Offset + i] ^= _keyBytes[i % _keyBytes.Length];
        }

        public override void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data) =>
            EncryptDecryptXor(data);

        public override void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data) =>
            EncryptDecryptXor(data);
    }
}