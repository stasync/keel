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
            internal int Count => _value.Count;

            private readonly Dictionary<IPEndPoint, EndPointDataByProtocolCollection> _value = new();
            private readonly List<IPEndPoint> _inactiveEndPointToRemove = new();

            /// <summary>
            /// Returns the per protocol state for the sender.
            /// </summary>
            internal EndPointData Get(in IncomingDataSnapshot incomingData)
            {
                if (!_value.TryGetValue(incomingData.EndPoint, out var endPointData))
                    _value.Add(incomingData.EndPoint, endPointData = new EndPointDataByProtocolCollection());

                // Any datagram from this endpoint keeps its tracking state alive.
                endPointData.LastActivityTime = DateTime.UtcNow;

                return endPointData.Get((DgramDeliveryMethod)incomingData.ProtocolPrefix);
            }

            /// <summary>
            /// Collects the endpoints that have been silent for longer than the timeout and drops their
            /// tracking state. State is allocated on the first datagram from any source address, so without
            /// this a peer that never becomes a connection would hold it forever.
            /// </summary>
            internal IReadOnlyList<IPEndPoint> RemoveInactive(DateTime currentTime, double inactivityTimeoutMs)
            {
                // Collected first, then removed.
                _inactiveEndPointToRemove.Clear();

                foreach (var (endpoint, protocolData) in _value)
                {
                    var lifeSpan = currentTime - protocolData.LastActivityTime;
                    if (lifeSpan.TotalMilliseconds > inactivityTimeoutMs)
                        _inactiveEndPointToRemove.Add(endpoint);
                }

                foreach (var inactiveEndPoint in _inactiveEndPointToRemove)
                    Remove(inactiveEndPoint);

                return _inactiveEndPointToRemove;
            }

            internal void Remove(IPEndPoint endPoint)
            {
                _value.Remove(endPoint);
#if NET9_0_OR_GREATER
                if (_value.Count == 0 || _value.Capacity - _value.Count > 64)
                    _value.TrimExcess();
#endif
            }

            internal void Clear()
            {
                _value.Clear();
                _inactiveEndPointToRemove.Clear();
            }
        }

        private sealed class EndPointDataByProtocolCollection
        {
            /// <summary>
            /// When a datagram was last seen from this endpoint, used to expire the state.
            /// </summary>
            internal DateTime LastActivityTime { get; set; }

            private readonly EndPointData[] _value;

            internal EndPointDataByProtocolCollection()
            {
                var enumValueCount = Enum.GetNames(typeof(DgramDeliveryMethod)).Length;
                _value = new EndPointData[enumValueCount];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal EndPointData Get(DgramDeliveryMethod protocol) =>
                _value[(int)protocol] ?? (_value[(int)protocol] = new EndPointData());
        }

        private sealed class EndPointData
        {
            private uint _lastValidUid;
            private bool _hasOrderedBaseline;

            private readonly DuplicateTracker _receivedDatagrams = new();
            private readonly List<IncomingDataSnapshot> _unorderedPendingData = new();

            internal void TryProcess(in IncomingDataSnapshot data, Queue<IncomingDataSnapshot> incomingQueue)
            {
                // Ignore duplicates at this point.
                if (_receivedDatagrams.IsDuplicate(data.Uid))
                    return;

                var deliveryMethod = (DgramDeliveryMethod)data.ProtocolPrefix;

                // Anchor the ordered stream to the first uid seen instead of expecting it to start from 1.
                // State expires on inactivity while the sender keeps counting, so a peer that goes quiet and
                // comes back would stall forever waiting for uids it has already sent.
                //
                // Say the sender is at 41 and the state has just expired, so the cursor is back to 0:
                //   42 arrives - 0 + 1 is not 42, so it goes to the pending buffer,
                //   43 arrives - still waiting for 1, pending,
                //   44 arrives - pending, and so on.
                // 1 to 41 have already been delivered and will never be sent again, so nothing in the buffer
                // ever becomes deliverable. Anchoring to 41 instead delivers 42, then 43, 44 as usual.
                //
                // NOTE: uids start from 1 - a zero would underflow the anchor, so the check below rejects it.
                if (deliveryMethod == DgramDeliveryMethod.ReliableOrdered && !_hasOrderedBaseline && data.Uid > 0)
                {
                    _hasOrderedBaseline = true;
                    _lastValidUid = data.Uid - 1;
                }

                if (deliveryMethod == DgramDeliveryMethod.ReliableOrdered && _lastValidUid > data.Uid)
                {
                    // This is more or less a sanity check, for ReliableOrdered protocol only.
                    // NOTE: This is a serious reliability issue.
                    throw new InvalidOperationException($"[{(DgramDeliveryMethod)data.ProtocolPrefix}] Incorrect incoming datagram uid: '{data.Uid}', but expected id should be more than '{_lastValidUid}'.");
                }

                var shouldBeReceived = true;

                // If the message should be received in order, check the previous message uid - it should be less by 1.
                // Otherwise, add it to the pending buffer.
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

                    // Try to free up memory if the capacity-to-length diff is getting bigger.
                    if (_unorderedPendingData.Count == 0 || _unorderedPendingData.Capacity - _unorderedPendingData.Count >= 64)
                        _unorderedPendingData.TrimExcess();
                }

                return dataIndex != -1;
            }
        }

        private sealed class DuplicateTracker
        {
            private const int BITS_PER_WORD = sizeof(ulong) * 8;
            private const int WINDOW_WORD_COUNT = 32;
            private const int WINDOW_SIZE = WINDOW_WORD_COUNT * BITS_PER_WORD;
            private const uint WINDOW_INDEX_MASK = WINDOW_SIZE - 1;

            // Circular bitset - the datagram 'uid' is tracked by the bit at 'uid % WINDOW_SIZE'. The window is anchored
            // to the newest datagram seen, but its contents never move: advancing only has to clear the slots the window
            // moved onto, which is a single bit for the usual case of the next datagram in sequence.
            private readonly ulong[] _receivedPackets = new ulong[WINDOW_WORD_COUNT];

            private uint _highestSequenceNumber;

            internal bool IsDuplicate(uint sequenceNumber)
            {
                // The newest datagram so far - move the window up to it.
                // NOTE: Datagram uids start from 1, so the initial state always takes this path.
                if (sequenceNumber > _highestSequenceNumber)
                {
                    AdvanceTo(sequenceNumber);
                    return false;
                }

                // Older than the window can remember. A late arrival is indistinguishable from a duplicate at this point.
                if (_highestSequenceNumber - sequenceNumber >= WINDOW_SIZE)
                    return true;

                if (IsReceived(sequenceNumber))
                    return true;

                SetReceived(sequenceNumber);
                return false;
            }

            private void AdvanceTo(uint sequenceNumber)
            {
                var advancedBy = sequenceNumber - _highestSequenceNumber;

                // The window moved past everything it held, so no mark is worth keeping.
                if (advancedBy >= WINDOW_SIZE)
                    Array.Clear(_receivedPackets, index: 0, length: WINDOW_WORD_COUNT);
                // Every slot between the old and the new head still holds the state of the datagram that just fell out
                // of the window. The next datagram in the sequence skips this entirely - it lands on the only slot we are
                // about to set anyway.
                else if (advancedBy > 1)
                    ClearRange(_highestSequenceNumber + 1, advancedBy - 1);

                _highestSequenceNumber = sequenceNumber;
                SetReceived(sequenceNumber);
            }

            /// <summary>
            /// Clears <paramref name="count"/> consecutive slots starting at <paramref name="from"/>, wrapping around
            /// the end of the bitset. Clears whole words at a time, so the cost is bound by the word count rather than
            /// by how far the window moved.
            /// </summary>
            private void ClearRange(uint from, uint count)
            {
                var index = from & WINDOW_INDEX_MASK;
                var word = (int)(index / BITS_PER_WORD);
                var bit = (int)(index % BITS_PER_WORD);

                while (count > 0)
                {
                    var bitsInThisWord = Math.Min((int)count, BITS_PER_WORD - bit);

                    // A full word cannot be expressed as a shifted mask - '1UL << 64' is not a zero shift here.
                    var mask = bitsInThisWord == BITS_PER_WORD ? ulong.MaxValue : ((1UL << bitsInThisWord) - 1) << bit;

                    _receivedPackets[word] &= ~mask;

                    count -= (uint)bitsInThisWord;
                    bit = 0;
                    word = word + 1 == WINDOW_WORD_COUNT ? 0 : word + 1;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsReceived(uint sequenceNumber)
            {
                var index = sequenceNumber & WINDOW_INDEX_MASK;
                return (_receivedPackets[index / BITS_PER_WORD] & (1UL << (int)(index % BITS_PER_WORD))) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetReceived(uint sequenceNumber)
            {
                var index = sequenceNumber & WINDOW_INDEX_MASK;
                _receivedPackets[index / BITS_PER_WORD] |= 1UL << (int)(index % BITS_PER_WORD);
            }
        }

        /// <summary>
        /// How long an endpoint may stay silent before its tracking state is released. Twice
        /// <see cref="UdpReliableProtocol.MAX_RESEND_DURATION_MS"/>, so the state always outlives any
        /// retransmission still in flight for that endpoint.
        /// </summary>
        private const double ENDPOINT_INACTIVITY_TIMEOUT_MS = UdpReliableProtocol.MAX_RESEND_DURATION_MS * 2;

        /// <summary>
        /// How often the inactivity sweep runs. Walking every tracked endpoint on every poll would be wasted
        /// work, so entries live somewhere between one and two intervals past the timeout.
        /// </summary>
        private const double ENDPOINT_SWEEP_INTERVAL_MS = UdpReliableProtocol.MAX_RESEND_DURATION_MS;

        public int ReliabilityFailureCount =>
            _baseProtocol.ReliabilityFailureCount;

        /// <summary>
        /// How many endpoints currently hold tracking state. State is allocated on the first datagram from any
        /// source address, so this grows with unsolicited traffic and not just with real connections.
        /// </summary>
        public int TrackedEndPointCount =>
            _trackedEndPoints.Count;

        public event Action<IPEndPoint> FailedToProcessDgram = delegate { };

        private readonly Queue<IncomingDataSnapshot> _incomingPayloadQueue = new();
        private readonly EndPointDataCollection _trackedEndPoints = new();
        private readonly UdpReliableProtocol _baseProtocol;

        private DateTime _nextEndPointSweepTime;

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

            TryRemoveInactiveEndPoints();
        }

        /// <summary>
        /// Releases tracking state for endpoints that have gone quiet.
        ///
        /// Endpoints that become connections are cleaned up explicitly by the caller, but anything that only
        /// ever sends a datagram or two - a scan, a spoofed source, an abandoned handshake - is only ever
        /// released here.
        /// </summary>
        private void TryRemoveInactiveEndPoints()
        {
            var currentTime = DateTime.UtcNow;
            if (currentTime < _nextEndPointSweepTime)
                return;

            _nextEndPointSweepTime = currentTime.AddMilliseconds(ENDPOINT_SWEEP_INTERVAL_MS);

            var inactiveEndPoints = _trackedEndPoints.RemoveInactive(currentTime, ENDPOINT_INACTIVITY_TIMEOUT_MS);
            foreach (var inactiveEndPoint in inactiveEndPoints)
                _baseProtocol.ClearEndpointData(inactiveEndPoint);
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