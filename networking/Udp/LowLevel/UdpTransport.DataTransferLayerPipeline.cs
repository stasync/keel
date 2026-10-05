using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Keel.Networking.Udp.LowLevel
{
    public sealed partial class UdpTransport
    {
        /// <summary>
        /// A collection of <see cref="DataTransferLayer"/>.
        /// Runs at the lowest level of <see cref="UdpTransport"/>.
        /// </summary>
        private sealed class DataTransferLayerPipeline
        {
            internal int LayersCount => _layers.Count;

            private readonly List<DataTransferLayer> _layers = new();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ProcessOutgoingData(IPEndPoint targetEndPoint, ref ArraySegment<byte> data)
            {
                foreach (var layer in _layers)
                {
                    layer.ProcessOutgoingData(targetEndPoint, ref data);

                    if (data.Array == null)
                        ThrowBackingArrayBecomesNull(layer);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ProcessIncomingData(IPEndPoint senderEndPoint, ref ArraySegment<byte> data)
            {
                // Upon received, we should go through layers in reverse, just to keep a consistency between operations.
                // For example - encryption layer is usually the last layer that processes OUTGOING messages, meaning that it should be the 1st layer that processes INCOMING ones.
                for (var index = _layers.Count - 1; index >= 0; index--)
                {
                    _layers[index].ProcessIncomingData(senderEndPoint, ref data);

                    if (data.Array == null)
                        ThrowBackingArrayBecomesNull(_layers[index]);
                }
            }

            internal void RegisterLayer(DataTransferLayer value)
            {
                if (value != null)
                    _layers.Add(value);
            }

            internal void UnregisterLayer(DataTransferLayer value) =>
                _layers.Remove(value);

            internal void Clear() =>
                _layers.Clear();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void ThrowBackingArrayBecomesNull(DataTransferLayer layer) =>
                throw new InvalidOperationException($"Internal data segment array was set to null while executing layer of type '{layer.GetType().FullName}'! Make sure you aren't attempting to assign a default state to the data reference (data = default(ArraySegment<byte>)).");
        }

        public void RegisterDataTransferLayer(DataTransferLayer value)
        {
            _dataTransferLayerPipeline ??= new DataTransferLayerPipeline();
            _dataTransferLayerPipeline.RegisterLayer(value);
        }

        public void UnregisterDataTransferLayer(DataTransferLayer value)
        {
            if (_dataTransferLayerPipeline == null)
                return;

            _dataTransferLayerPipeline.UnregisterLayer(value);
            if (_dataTransferLayerPipeline.LayersCount == 0)
                _dataTransferLayerPipeline = null;
        }

        public void UnregisterAllDataTransferLayers()
        {
            _dataTransferLayerPipeline?.Clear();
            _dataTransferLayerPipeline = null;
        }
    }
}