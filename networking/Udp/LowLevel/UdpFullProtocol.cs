using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel
{
    /// <summary>
    /// Build on top of <see cref="UdpReliableProtocol"/>.
    /// Implements complete wrapper for <see cref="DgramDeliveryMethod"/> enum.
    /// Can filter out dgram duplicates.
    /// Can receive dgram in order.
    /// </summary>
    public sealed class UdpFullProtocol : IDisposable
    {
        public enum DgramDeliveryMethod
        {
            Unreliable = 0,
            Reliable,
            ReliableOrdered
        }

        private sealed class EndPointDataCollection
        {
            private readonly Dictionary<IPEndPoint, EndPointDataByProtocolCollection> _value = new();

            internal EndPointData Get(in IncomingDataSnapshot incomingData)
            {
                if (!_value.TryGetValue(incomingData.EndPoint, out var endPointData))
                    _value.Add(incomingData.EndPoint, endPointData = new EndPointDataByProtocolCollection());

                return endPointData.Get((DgramDeliveryMethod)incomingData.ProtocolPrefix);
            }

            internal void Remove(IPEndPoint endPoint)
            {
                _value.Remove(endPoint);
#if NET9_0_OR_GREATER
                if (_value.Count == 0 || _value.Capacity - _value.Count > 64)
                    _value.TrimExcess();
#endif
            }

            internal void Clear() =>
                _value.Clear();
        }

        private sealed class EndPointDataByProtocolCollection
        {
            private readonly EndPointData[] _value;

            internal EndPointDataByProtocolCollection()
            {
                var enumValueCount = Enum.GetNames(typeof(DgramDeliveryMethod)).Length;
                _value = new EndPointData[enumValueCount];
            }

            internal EndPointData Get(DgramDeliveryMethod protocol) =>
                _value[(int)protocol] ?? (_value[(int)protocol] = new EndPointData());
        }

        private sealed class EndPointData
        {
            private uint _lastValidUid;

            private readonly DuplicateTracker _receivedDatagrams = new();
            private readonly List<IncomingDataSnapshot> _unorderedPendingData = new();

            internal void TryProcess(in IncomingDataSnapshot data, Queue<IncomingDataSnapshot> incomingQueue)
            {
                // Ignore duplicates at this point.
                if (_receivedDatagrams.IsDuplicate(data.Uid))
                    return;

                var deliveryMethod = (DgramDeliveryMethod)data.ProtocolPrefix;
                if (deliveryMethod == DgramDeliveryMethod.ReliableOrdered && _lastValidUid > data.Uid)
                {
                    // This is more or less sanity check, for ReliableOrdered protocol only.
                    // NOTE: This is a serious reliability issue.
                    throw new InvalidOperationException($"[{(DgramDeliveryMethod)data.ProtocolPrefix}] Incorrect incoming datagram uid: '{data.Uid}', but expected id should be more than '{_lastValidUid}'.");
                }

                var shouldBeReceived = true;

                // If message should be received in order, check previous message uid - it should be less by 1.
                // Otherwise, add it to pending buffer.
                if (deliveryMethod == DgramDeliveryMethod.ReliableOrdered && _lastValidUid != data.Uid - 1)
                {
                    _unorderedPendingData.Add(data);
                    shouldBeReceived = false;
                }

                if (shouldBeReceived)
                {
                    // Append current message to queue.
                    EnqueueReceive(data, incomingQueue);

                    // If this message is received in order, try to find pending ordered messages and append to queue.
                    if (deliveryMethod == DgramDeliveryMethod.ReliableOrdered)
                    {
                        while (TryDequeuePending(out var snapshot))
                            EnqueueReceive(snapshot, incomingQueue);
                    }
                }
            }

            private void EnqueueReceive(in IncomingDataSnapshot data, Queue<IncomingDataSnapshot> incomingQueue)
            {
                _lastValidUid = data.Uid;
                incomingQueue.Enqueue(data);
            }

            private bool TryDequeuePending(out IncomingDataSnapshot result)
            {
                result = default;
                var dataIndex = -1;

                for (var i = 0; i < _unorderedPendingData.Count; i++)
                {
                    var data = _unorderedPendingData[i];
                    if (data.Uid != _lastValidUid + 1)
                        continue;

                    dataIndex = i;
                    result = data;
                    break;
                }

                if (dataIndex != -1)
                {
                    _unorderedPendingData.RemoveAt(dataIndex);

                    // Try free up memory, if capacity to length diff is getting bigger.
                    if (_unorderedPendingData.Count == 0 || _unorderedPendingData.Capacity - _unorderedPendingData.Count >= 64)
                        _unorderedPendingData.TrimExcess();
                }

                return dataIndex != -1;
            }
        }

        private sealed class DuplicateTracker
        {
            private const int WINDOW_SIZE = 256;
            private const int WINDOW_SIZE_ADVANCE_VALUE = WINDOW_SIZE / 4;

            private static readonly byte[] s_emptyShiftArray = new byte[WINDOW_SIZE];

            private uint _baseSequenceNumber;
            private readonly byte[] _receivedPackets = new byte[WINDOW_SIZE];

            internal bool IsDuplicate(uint sequenceNumber)
            {
                // If sequence number is too old.
                if (sequenceNumber < _baseSequenceNumber)
                    return true;

                // If sequence number is too far ahead, we need to adjust our window.
                if (sequenceNumber >= _baseSequenceNumber + WINDOW_SIZE)
                    AdvanceWindow(newBaseSequenceNumber: sequenceNumber - WINDOW_SIZE + WINDOW_SIZE_ADVANCE_VALUE);

                var index = (int)(sequenceNumber - _baseSequenceNumber);
                if (_receivedPackets[index] == 1)
                    return true;

                _receivedPackets[index] = 1;
                return false;
            }

            private void AdvanceWindow(uint newBaseSequenceNumber)
            {
                var shiftAmount = newBaseSequenceNumber - _baseSequenceNumber;
                if (shiftAmount > WINDOW_SIZE)
                    shiftAmount %= WINDOW_SIZE;

                if (shiftAmount != WINDOW_SIZE)
                {
                    Buffer.BlockCopy(
                        src: _receivedPackets,
                        srcOffset: (int)shiftAmount,
                        dst: _receivedPackets,
                        dstOffset: 0,
                        count: WINDOW_SIZE - (int)shiftAmount);

                    Buffer.BlockCopy(
                        src: s_emptyShiftArray,
                        srcOffset: 0,
                        dst: _receivedPackets,
                        dstOffset: WINDOW_SIZE - (int)shiftAmount,
                        count: (int)shiftAmount);
                }

                _baseSequenceNumber = newBaseSequenceNumber;
            }
        }

        public int ReliabilityFailureCount =>
            _baseProtocol.ReliabilityFailureCount;

        public event Action<IPEndPoint> FailedToProcessDgram = delegate { };

        private readonly Queue<IncomingDataSnapshot> _incomingPayloadQueue = new();
        private readonly EndPointDataCollection _trackedEndPoints = new();
        private readonly UdpReliableProtocol _baseProtocol;

        public UdpFullProtocol(int port, ushort protocolKey)
        {
            _baseProtocol = new UdpReliableProtocol(port, protocolKey);
            _baseProtocol.FailedToProcessDgram += OnFailedToProcessDgram;
        }

        private void OnFailedToProcessDgram(IPEndPoint sender) =>
            FailedToProcessDgram(sender);

        public void Poll()
        {
            _baseProtocol.Poll();

            while (_baseProtocol.TryDequeueIncoming(out var incomingData))
            {
                var endPointData = _trackedEndPoints.Get(incomingData);
                endPointData.TryProcess(incomingData, _incomingPayloadQueue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeueIncoming(out IncomingDataSnapshot incomingData)
        {
            var canDequeue = _incomingPayloadQueue.Count > 0;
            incomingData = canDequeue ? _incomingPayloadQueue.Dequeue() : default;
            return canDequeue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ValidateDataSize(ArraySegment<byte> data) =>
            _baseProtocol.ValidateDataSize(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendTo(IPEndPoint endPoint, ArraySegment<byte> data, DgramDeliveryMethod deliveryMethod) =>
            _baseProtocol.SendTo(endPoint, data, (byte)deliveryMethod);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEndPointData(IPEndPoint endPoint)
        {
            _trackedEndPoints.Remove(endPoint);
            _baseProtocol.ClearEndpointData(endPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlacklisted(IPAddress address) =>
            _baseProtocol.IsBlacklisted(address);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdateAddressInBlacklist(IPAddress address, int millisecondsToAdd) =>
            _baseProtocol.AddOrUpdateAddressInBlacklist(address, millisecondsToAdd);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterIncomingPacketSimulator(UdpReliableProtocol.IncomingPacketSimulator value) =>
            _baseProtocol.RegisterIncomingPacketSimulator(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterIncomingPacketSimulator(UdpReliableProtocol.IncomingPacketSimulator value) =>
            _baseProtocol.UnregisterIncomingPacketSimulator(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterAllIncomingPacketSimulators() =>
            _baseProtocol.UnregisterAllIncomingPacketSimulators();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterDataTransferLayer(UdpReliableProtocol.DataTransferLayer value) =>
            _baseProtocol.RegisterDataTransferLayer(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterDataTransferLayer(UdpReliableProtocol.DataTransferLayer value) =>
            _baseProtocol.UnregisterDataTransferLayer(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnregisterAllDataTransferLayers() =>
            _baseProtocol.UnregisterAllDataTransferLayers();

        public void Dispose()
        {
            _baseProtocol.Dispose();
            _trackedEndPoints.Clear();
        }
    }
}