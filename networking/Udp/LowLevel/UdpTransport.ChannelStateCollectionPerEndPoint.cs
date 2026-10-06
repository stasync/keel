using Keel.Networking.Udp.LowLevel.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Keel.Networking.Udp.LowLevel
{
    public partial class UdpTransport
    {
        internal sealed class ChannelStateCollectionPerEndPoint
        {
            private readonly Dictionary<IPEndPoint, ChannelStateCollection> _value = new();
            private readonly Action<DatagramSnapshot> _resendRequired;
            private readonly Action<IPEndPoint> _reliabilityFailure;

            internal ChannelStateCollectionPerEndPoint(Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
            {
                _resendRequired = resendRequired;
                _reliabilityFailure = reliabilityFailure;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal uint ProcessOutgoingDatagram(IPEndPoint endPoint, byte channel, ArraySegment<byte> data)
            {
                if (!_value.TryGetValue(endPoint, out var channelStateCollection))
                    _value.Add(endPoint, channelStateCollection = new ChannelStateCollection(endPoint, channelCount: 8, _resendRequired, _reliabilityFailure));

                return channelStateCollection.ProcessOutgoingDatagram(channel, data);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Update()
            {
                foreach (var channelStateCollection in _value.Values)
                    channelStateCollection.Update();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReleasePendingOutgoingDatagram(IPEndPoint endPoint, uint uid, byte channel)
            {
                if (_value.TryGetValue(endPoint, out var channelStateCollection))
                    channelStateCollection.ReleasePendingOutgoingDatagram(uid, channel);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void RemoveEndPointData(IPEndPoint endPoint)
            {
                if (_value.Remove(endPoint, out var channelStateCollection))
                    channelStateCollection.Clear();

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