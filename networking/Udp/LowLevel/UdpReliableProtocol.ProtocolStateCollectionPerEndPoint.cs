using Core.Networking.Udp.LowLevel.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel
{
    public partial class UdpReliableProtocol
    {
        internal sealed class ProtocolStateCollectionPerEndPoint
        {
            private readonly Dictionary<IPEndPoint, ProtocolStateCollection> _value = new();
            private readonly Action<DatagramSnapshot> _resendRequired;
            private readonly Action<IPEndPoint> _reliabilityFailure;

            internal ProtocolStateCollectionPerEndPoint(Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
            {
                _resendRequired = resendRequired;
                _reliabilityFailure = reliabilityFailure;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal uint ProcessOutgoingDatagram(IPEndPoint endPoint, byte protocolPrefix, ArraySegment<byte> data)
            {
                if (!_value.TryGetValue(endPoint, out var dgramByProtocolPrefixAllocator))
                    _value.Add(endPoint, dgramByProtocolPrefixAllocator = new ProtocolStateCollection(endPoint, protocolPoolSize: 8, _resendRequired, _reliabilityFailure));

                return dgramByProtocolPrefixAllocator.ProcessOutgoingDatagram(protocolPrefix, data);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Update()
            {
                foreach (var protocolStateCollection in _value.Values)
                    protocolStateCollection.Update();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void TreReleasePendingOutgoingDgram(IPEndPoint endPoint, uint uid, byte protocolPrefix)
            {
                if (_value.TryGetValue(endPoint, out var protocolStateCollection))
                    protocolStateCollection.TreReleasePendingOutgoingDgram(uid, protocolPrefix);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ClearEndpointData(IPEndPoint endPoint)
            {
                if (_value.Remove(endPoint, out var protocolStateCollection))
                    protocolStateCollection.Clear();

#if NET9_0_OR_GREATER
                if (_value.Count == 0 || _value.Capacity - _value.Count > 64)
                    _value.TrimExcess();
#endif
            }

            internal void Clear() =>
                _value.Clear();
        }
    }
}