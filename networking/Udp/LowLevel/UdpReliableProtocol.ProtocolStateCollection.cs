using Core.Networking.Udp.LowLevel.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel
{
    public partial class UdpReliableProtocol
    {
        internal sealed class ProtocolStateCollection
        {
            private sealed class ProtocolState
            {
                private uint _currentDatagramUid;
                private readonly byte _prefix;
                private readonly IPEndPoint _endPoint;
                private readonly Action<DatagramSnapshot> _resendRequired;
                private readonly Action<IPEndPoint> _reliabilityFailure;
                private readonly Dictionary<uint, DatagramSnapshot> _pendingOutgoingSnapshots = new();
                private readonly Dictionary<uint, byte> _outgoingRetryAttempts = new();
                private readonly List<uint> _attemptsToRemoveAfterResend = new();

                internal ProtocolState(byte protocolPrefix, IPEndPoint endPoint, Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
                {
                    _prefix = protocolPrefix;
                    _endPoint = endPoint;
                    _resendRequired = resendRequired;
                    _reliabilityFailure = reliabilityFailure;
                }

                internal uint ProcessOutgoingDatagram(in ArraySegment<byte> data)
                {
                    var result = ++_currentDatagramUid;
                    // If prefix is greater than 0, we expect reliable message and have to keep datagram state temporary.
                    if (_prefix > 0)
                    {
                        var snapshot = new DatagramSnapshot(result, _endPoint, data, _prefix);
                        _pendingOutgoingSnapshots.TryAdd(snapshot.Uid, snapshot);
                        _outgoingRetryAttempts.TryAdd(snapshot.Uid, 0);
                    }

                    return result;
                }

                internal void Update()
                {
                    _attemptsToRemoveAfterResend.Clear();

                    foreach (var pendingSnapshot in _pendingOutgoingSnapshots.Values)
                    {
                        if (_outgoingRetryAttempts[pendingSnapshot.Uid] >= MAX_RESEND_ATTEMPTS)
                        {
                            _attemptsToRemoveAfterResend.Add(pendingSnapshot.Uid);
                            _reliabilityFailure(pendingSnapshot.EndPoint);
                            continue;
                        }

                        // Resend dgram.
                        _resendRequired(pendingSnapshot);
                        _outgoingRetryAttempts[pendingSnapshot.Uid]++;
                    }

                    foreach (var dgramUid in _attemptsToRemoveAfterResend)
                        TreReleasePendingOutgoingDgram(dgramUid);
                }

                internal void TreReleasePendingOutgoingDgram(uint uid)
                {
                    if (_pendingOutgoingSnapshots.Remove(uid, out var snapshot))
                        snapshot.Dispose();

                    _outgoingRetryAttempts.Remove(uid);

#if NET9_0_OR_GREATER
                    if (_pendingOutgoingSnapshots.Count == 0 || _pendingOutgoingSnapshots.Capacity - _pendingOutgoingSnapshots.Count > 64)
                        _pendingOutgoingSnapshots.TrimExcess();

                    if (_outgoingRetryAttempts.Count == 0 || _outgoingRetryAttempts.Capacity - _outgoingRetryAttempts.Count > 64)
                        _outgoingRetryAttempts.TrimExcess();
#endif
                }

                internal void Clear()
                {
                    foreach (var snapshot in _pendingOutgoingSnapshots.Values)
                        snapshot.Dispose();

                    _pendingOutgoingSnapshots.Clear();
                    _outgoingRetryAttempts.Clear();
                    _attemptsToRemoveAfterResend.Clear();

                    _pendingOutgoingSnapshots.TrimExcess();
                    _outgoingRetryAttempts.TrimExcess();
                    _attemptsToRemoveAfterResend.TrimExcess();
                }
            }

            private readonly IPEndPoint _endPoint;
            private readonly Action<DatagramSnapshot> _resendRequired;
            private readonly Action<IPEndPoint> _reliabilityFailure;
            private readonly ProtocolState[] _state;

            internal ProtocolStateCollection(IPEndPoint endPoint, byte protocolPoolSize, Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
            {
                _endPoint = endPoint;
                _resendRequired = resendRequired;
                _reliabilityFailure = reliabilityFailure;
                _state = new ProtocolState[protocolPoolSize];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal uint ProcessOutgoingDatagram(byte protocolPrefix, ArraySegment<byte> data)
            {
                ref var state = ref _state[protocolPrefix];
                state ??= new ProtocolState(protocolPrefix, _endPoint, _resendRequired, _reliabilityFailure);
                return state.ProcessOutgoingDatagram(data);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Update()
            {
                foreach (var state in _state)
                    state?.Update();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void TreReleasePendingOutgoingDgram(uint uid, byte protocolPrefix) =>
                _state[protocolPrefix].TreReleasePendingOutgoingDgram(uid);

            internal void Clear()
            {
                foreach (var state in _state)
                    state?.Clear();
            }
        }
    }
}