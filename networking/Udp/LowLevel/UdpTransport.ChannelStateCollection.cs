using Keel.Networking.Udp.LowLevel.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Keel.Networking.Udp.LowLevel
{
    public partial class UdpTransport
    {
        internal sealed class ChannelStateCollection
        {
            private sealed class ChannelState
            {
                /// <summary>
                /// Retransmission bookkeeping for a single pending datagram.
                /// </summary>
                private struct OutgoingRetryState
                {
                    internal DateTime GiveUpTime;
                    internal DateTime NextResendTime;
                    internal double CurrentResendDelayMs;
                }

                private uint _currentDatagramUid;
                private readonly byte _channel;
                private readonly IPEndPoint _endPoint;
                private readonly Action<DatagramSnapshot> _resendRequired;
                private readonly Action<IPEndPoint> _reliabilityFailure;
                private readonly Dictionary<uint, DatagramSnapshot> _pendingOutgoingSnapshots = new();
                private readonly Dictionary<uint, OutgoingRetryState> _outgoingRetryStates = new();
                private readonly List<uint> _attemptsToRemoveAfterResend = new();

                internal ChannelState(byte channel, IPEndPoint endPoint, Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
                {
                    _channel = channel;
                    _endPoint = endPoint;
                    _resendRequired = resendRequired;
                    _reliabilityFailure = reliabilityFailure;
                }

                internal uint ProcessOutgoingDatagram(in ArraySegment<byte> data)
                {
                    var result = ++_currentDatagramUid;
                    // If the channel is greater than 0, we expect a reliable message and have to keep the datagram state temporarily.
                    if (_channel > 0)
                    {
                        var snapshot = new DatagramSnapshot(result, _endPoint, data, _channel);
                        _pendingOutgoingSnapshots.TryAdd(snapshot.Uid, snapshot);

                        // The datagram has just gone out, so the first retransmission is one delay away rather
                        // than due on the very next tick.
                        var sentTime = DateTime.UtcNow;
                        var retryState = new OutgoingRetryState
                        {
                            CurrentResendDelayMs = INITIAL_RESEND_DELAY_MS,
                            NextResendTime = sentTime.AddMilliseconds(INITIAL_RESEND_DELAY_MS),
                            GiveUpTime = sentTime.AddMilliseconds(MAX_RESEND_DURATION_MS)
                        };
                        _outgoingRetryStates.TryAdd(snapshot.Uid, retryState);
                    }

                    return result;
                }

                internal void Update()
                {
                    _attemptsToRemoveAfterResend.Clear();

                    var currentTime = DateTime.UtcNow;

                    foreach (var pendingSnapshot in _pendingOutgoingSnapshots.Values)
                    {
#if NET9_0_OR_GREATER
                        // Get the retry state.
                        ref var retryState = ref System.Runtime.InteropServices
                            .CollectionsMarshal
                            .GetValueRefOrNullRef(_outgoingRetryStates, pendingSnapshot.Uid);

#else
                        var retryState = _outgoingRetryStates[pendingSnapshot.Uid];
#endif
                        // Retried for long enough - give up and report it.
                        if (currentTime >= retryState.GiveUpTime)
                        {
                            _attemptsToRemoveAfterResend.Add(pendingSnapshot.Uid);
                            _reliabilityFailure(pendingSnapshot.EndPoint);
                            continue;
                        }

                        // Not due yet. Without this every unacked datagram would go out again on every single
                        // tick, so the cost of one lost datagram would scale with the caller's update rate.
                        if (currentTime < retryState.NextResendTime)
                            continue;

                        // Resend datagram.
                        _resendRequired(pendingSnapshot);

                        // Update retry state.
                        retryState.CurrentResendDelayMs = Math.Min(retryState.CurrentResendDelayMs * RESEND_DELAY_GROWTH_FACTOR, MAX_RESEND_DELAY_MS);
                        retryState.NextResendTime = currentTime.AddMilliseconds(retryState.CurrentResendDelayMs);

#if !NET9_0_OR_GREATER
                        _outgoingRetryStates[pendingSnapshot.Uid] = retryState;
#endif
                    }

                    foreach (var datagramUid in _attemptsToRemoveAfterResend)
                        ReleasePendingOutgoingDatagram(datagramUid);
                }

                internal void ReleasePendingOutgoingDatagram(uint uid)
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
            private readonly ChannelState[] _state;

            internal ChannelStateCollection(IPEndPoint endPoint, byte channelCount, Action<DatagramSnapshot> resendRequired, Action<IPEndPoint> reliabilityFailure)
            {
                _endPoint = endPoint;
                _resendRequired = resendRequired;
                _reliabilityFailure = reliabilityFailure;
                _state = new ChannelState[channelCount];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal uint ProcessOutgoingDatagram(byte channel, ArraySegment<byte> data)
            {
                ref var state = ref _state[channel];
                state ??= new ChannelState(channel, _endPoint, _resendRequired, _reliabilityFailure);
                return state.ProcessOutgoingDatagram(data);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Update()
            {
                foreach (var state in _state)
                    state?.Update();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReleasePendingOutgoingDatagram(uint uid, byte channel) =>
                _state[channel].ReleasePendingOutgoingDatagram(uid);

            internal void Clear()
            {
                foreach (var state in _state)
                    state?.Clear();
            }
        }
    }
}