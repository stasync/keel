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
                /// <summary>
                /// Retransmission bookkeeping for a single pending datagram.
                /// </summary>
                private struct OutgoingRetryState
                {
                    internal byte Attempts;
                    internal DateTime NextResendTime;
                    private double _currentResendDelayMs;

                    internal OutgoingRetryState(DateTime sentTime)
                    {
                        Attempts = 0;
                        _currentResendDelayMs = INITIAL_RESEND_DELAY_MS;
                        NextResendTime = sentTime.AddMilliseconds(INITIAL_RESEND_DELAY_MS);
                    }

                    /// <summary>
                    /// Records a retransmission and schedules the next one further out than the last.
                    /// </summary>
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    internal void OnResent(DateTime resentTime)
                    {
                        Attempts++;
                        _currentResendDelayMs = Math.Min(_currentResendDelayMs * RESEND_DELAY_GROWTH_FACTOR, MAX_RESEND_DELAY_MS);
                        NextResendTime = resentTime.AddMilliseconds(_currentResendDelayMs);
                    }
                }

                private uint _currentDatagramUid;
                private readonly byte _prefix;
                private readonly IPEndPoint _endPoint;
                private readonly Action<DatagramSnapshot> _resendRequired;
                private readonly Action<IPEndPoint> _reliabilityFailure;
                private readonly Dictionary<uint, DatagramSnapshot> _pendingOutgoingSnapshots = new();
                private readonly Dictionary<uint, OutgoingRetryState> _outgoingRetryStates = new();
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
                    // If prefix is greater than 0, we expect a reliable message and have to keep the datagram state temporary.
                    if (_prefix > 0)
                    {
                        var snapshot = new DatagramSnapshot(result, _endPoint, data, _prefix);
                        _pendingOutgoingSnapshots.TryAdd(snapshot.Uid, snapshot);

                        // The datagram has just gone out, so the first retransmission is one delay away rather
                        // than due on the very next tick.
                        _outgoingRetryStates.TryAdd(snapshot.Uid, new OutgoingRetryState(DateTime.UtcNow));
                    }

                    return result;
                }

                internal void Update()
                {
                    _attemptsToRemoveAfterResend.Clear();

                    var currentTime = DateTime.UtcNow;

                    foreach (var pendingSnapshot in _pendingOutgoingSnapshots.Values)
                    {
                        var retryState = _outgoingRetryStates[pendingSnapshot.Uid];

                        if (retryState.Attempts >= MAX_RESEND_ATTEMPTS)
                        {
                            _attemptsToRemoveAfterResend.Add(pendingSnapshot.Uid);
                            _reliabilityFailure(pendingSnapshot.EndPoint);
                            continue;
                        }

                        // Not due yet. Without this every unacked datagram would go out again on every single
                        // tick, so the cost of one lost datagram would scale with the caller's update rate.
                        if (currentTime < retryState.NextResendTime)
                            continue;

                        // Resend dgram.
                        _resendRequired(pendingSnapshot);

                        retryState.OnResent(currentTime);
                        _outgoingRetryStates[pendingSnapshot.Uid] = retryState;
                    }

                    foreach (var dgramUid in _attemptsToRemoveAfterResend)
                        TreReleasePendingOutgoingDgram(dgramUid);
                }

                internal void TreReleasePendingOutgoingDgram(uint uid)
                {
                    if (_pendingOutgoingSnapshots.Remove(uid, out var snapshot))
                        snapshot.Dispose();

                    _outgoingRetryStates.Remove(uid);

#if NET9_0_OR_GREATER
                    if (_pendingOutgoingSnapshots.Count == 0 || _pendingOutgoingSnapshots.Capacity - _pendingOutgoingSnapshots.Count > 64)
                        _pendingOutgoingSnapshots.TrimExcess();

                    if (_outgoingRetryStates.Count == 0 || _outgoingRetryStates.Capacity - _outgoingRetryStates.Count > 64)
                        _outgoingRetryStates.TrimExcess();
#endif
                }

                internal void Clear()
                {
                    foreach (var snapshot in _pendingOutgoingSnapshots.Values)
                        snapshot.Dispose();

                    _pendingOutgoingSnapshots.Clear();
                    _outgoingRetryStates.Clear();
                    _attemptsToRemoveAfterResend.Clear();

                    _pendingOutgoingSnapshots.TrimExcess();
                    _outgoingRetryStates.TrimExcess();
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