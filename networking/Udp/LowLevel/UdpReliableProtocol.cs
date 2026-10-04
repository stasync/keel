using Keel.Networking.Udp.LowLevel.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Keel.Networking.Udp.LowLevel
{
    /// <summary>
    /// Basic (un)reliable upd protocol.
    /// !!! May receive duplicated data due to reliability.
    /// !!! May receive unordered data.
    /// </summary>
    public sealed partial class UdpReliableProtocol : IDisposable
    {
        /// <summary>
        /// How long an unacknowledged datagram keeps being retransmitted before it is given up on and reported
        /// as a reliability failure.
        ///
        /// Expressed as a duration rather than an attempt count on purpose: with the growing resend delay
        /// below, a fixed number of attempts would mean a give up window that silently moves whenever those
        /// delays are returned.
        ///
        /// This is also the source for <see cref="ReliableUdpServer.HEARTBEAT_TIMEOUT_MS"/> - there is no
        /// point retransmitting for longer than the connection above would survive without a heartbeat.
        /// </summary>
        public const uint MAX_RESEND_DURATION_MS = 2000;

        /// <summary>
        /// How long to wait after the initial sending before the first retransmission.
        /// </summary>
        private const double INITIAL_RESEND_DELAY_MS = 100;

        /// <summary>
        /// The delay grows by this factor with every attempt, so a peer that is not answering is probed less
        /// and less often instead of on every single update tick.
        /// </summary>
        private const double RESEND_DELAY_GROWTH_FACTOR = 2;

        /// <summary>
        /// Upper bound on the grown delay, so a long-lived datagram keeps retrying at a steady rate rather
        /// than drifting towards never.
        /// </summary>
        private const double MAX_RESEND_DELAY_MS = 1000;

        public event Action<IPEndPoint> FailedToProcessDgram = delegate { };

        public int ReliabilityFailureCount { get; private set; }

        private readonly Socket _socket;
        private readonly ProtocolStateCollectionPerEndPoint _dgramStatePerEndPoint;
        private readonly Queue<IncomingDataSnapshot> _incomingPayloadQueue = new();
        private readonly byte[] _incomingBuffer = new byte[MtuBuffer.SIZE];
        private readonly byte[] _outgoingBuffer = new byte[MtuBuffer.SIZE];
        private readonly ushort _protocolKey;
        private readonly int _protocolHeaderSize;

        private IncomingPacketSimulationPipeline _incomingPacketSimulationPipeline;
        private DataTransferLayerPipeline _dataTransferLayerPipeline;
        private IpAddressBlacklist _ipAddressBlacklist;

        public UdpReliableProtocol(int port, ushort protocolKey)
        {
            _dgramStatePerEndPoint = new ProtocolStateCollectionPerEndPoint(OnResendRequired, OnReliabilityFailure);

            _socket = UdpUtils.CreateUdpSocket();
            if (port != 0)
                _socket.Bind(new IPEndPoint(IPAddress.Any, port));

            _protocolKey = protocolKey;

            // Determining protocol header size by just writing the reader. Parameters don't matter here.
            // The value is used for validation on the higher abstraction level.
            _protocolHeaderSize = PrepareOutgoingBufferProtocolHeader(_outgoingBuffer, datagramType: default, protocolKey: 0, protocolPrefix: 0, dgramUid: 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Poll()
        {
            ReceiveDgram();
            _dgramStatePerEndPoint.Update();
            _ipAddressBlacklist?.Update();
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
            data.Count + _protocolHeaderSize <= MtuBuffer.SIZE;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendTo(IPEndPoint endPoint, ArraySegment<byte> data, byte protocolPrefix)
        {
            if (!ValidateDataSize(data))
                throw new InvalidOperationException($"Mtu buffer of size '{MtuBuffer.SIZE}' with maximum payload size of '{MtuBuffer.SIZE - _protocolHeaderSize}' overflow with '{data.Count}'.");

            var dgramUid = _dgramStatePerEndPoint.ProcessOutgoingDatagram(endPoint, protocolPrefix, data);
            SendPayloadTo_Internal(endPoint, data, dgramUid, protocolPrefix);
        }

        private void OnReliabilityFailure(IPEndPoint endPoint)
        {
            // TODO: Make event instead?
            ReliabilityFailureCount++;
        }

        private void OnResendRequired(DatagramSnapshot pendingSnapshot) =>
            SendPayloadTo_Internal(pendingSnapshot.EndPoint, pendingSnapshot.Buffer, pendingSnapshot.Uid, pendingSnapshot.ProtocolPrefix);

        private void ReceiveDgram()
        {
            if (!_socket.IsBound)
                return;

            while (_socket.Available > 0)
            {
                EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, port: 0);
                var received = UdpUtils.ReceiveMtuFrom(_socket, _incomingBuffer, ref senderEndPoint);

                // Nothing has been received, or a socket exception was thrown.
                // NOTE: We have to continue the loop here, not brake.
                if (received == 0)
                    continue;

                var senderIpEndpoint = (IPEndPoint)senderEndPoint;

                // Making an initial data slice and pass it through to the layering pipeline.
                var receivedDataSegment = new ArraySegment<byte>(_incomingBuffer, offset: 0, count: received);
                _dataTransferLayerPipeline?.ProcessIncomingData(senderIpEndpoint, ref receivedDataSegment);

                if (IsBlacklisted(senderIpEndpoint.Address))
                    continue;

                if (_incomingPacketSimulationPipeline != null)
                {
                    // Slow track - though simulators.
                    // We have to copy the data here, as the simulator may aggregate packages and 'receivedDataSegment' uses the shared incoming buffer.
                    var dataCopy = new byte[receivedDataSegment.Count];
                    Buffer.BlockCopy(src: receivedDataSegment.Array!, srcOffset: receivedDataSegment.Offset, dst: dataCopy, dstOffset: 0, count: receivedDataSegment.Count);

                    // Dispatch data copy to the simulation pipeline.
                    _incomingPacketSimulationPipeline.OnDataReceived(dataCopy, senderIpEndpoint);
                }
                else
                {
                    // Fast track - packet allowed to be processed as there are no simulators declared.
                    ProcessIncomingData(senderIpEndpoint, receivedDataSegment);
                }
            }

            // Receive packets form the simulator(s).
            if (_incomingPacketSimulationPipeline != null)
            {
                // Update the simulation pipeline.
                _incomingPacketSimulationPipeline.Update();

                // Dequeue all incoming packets from the simulation pipeline. 
                while (_incomingPacketSimulationPipeline.TryDequeuePacket(out (byte[] data, IPEndPoint sender) packet))
                {
                    // No need to copy data here, as the data we get from the queue was already copied (when it was added to the queue).
                    ProcessIncomingData(packet.sender, data: new ArraySegment<byte>(packet.data));
                }
            }
        }

        private void ProcessIncomingData(IPEndPoint senderIpEndpoint, in ArraySegment<byte> data)
        {
            if (data.Count == 0)
                throw new InvalidOperationException();

            // Validate minimum packet size to prevent overflow when reading protocol header.
            if (data.Count < _protocolHeaderSize)
            {
                FailedToProcessDgram(senderIpEndpoint);
                return;
            }

            var protocolOffset = 0;
            var protocolKeyAsBytes = default(UShortByteUnion);
            protocolKeyAsBytes.Byte0 = data[protocolOffset++];
            protocolKeyAsBytes.Byte1 = data[protocolOffset++];

            var remoteProtocolKey = protocolKeyAsBytes.UShort;
            if (remoteProtocolKey != _protocolKey)
                return;

            var dgramType = (DatagramType)data[protocolOffset++];
            var protocolPrefix = data[protocolOffset++];

            var dgramUidAsBytes = default(UIntByteUnion);
            dgramUidAsBytes.Byte0 = data[protocolOffset++];
            dgramUidAsBytes.Byte1 = data[protocolOffset++];
            dgramUidAsBytes.Byte2 = data[protocolOffset++];
            dgramUidAsBytes.Byte3 = data[protocolOffset++];

            var dgramUid = dgramUidAsBytes.UInt;

            switch (dgramType)
            {
                case DatagramType.Payload:
                    {
                        // Payload received, send ack in case dgram id is valid.
                        // protocolPrefix should > 0, meaning that we expect reliable delivery.
                        if (protocolPrefix > 0)
                            SendAckTo_Internal(senderIpEndpoint, dgramUid, protocolPrefix);

                        // Validate payload size to prevent overflow.
                        var payloadSize = data.Count - protocolOffset;
                        if (payloadSize < 0)
                        {
                            FailedToProcessDgram(senderIpEndpoint);
                            return;
                        }

                        // Create a payload instance. We have to copy the data here as its unknown when the data will be consumed (form the outgoing queue by consumer).
                        var payload = new byte[payloadSize];
                        Buffer.BlockCopy(src: data.Array!, srcOffset: data.Offset + protocolOffset, dst: payload, dstOffset: 0, count: payload.Length);

                        // Append the data snapshot the then incoming data queue.
                        _incomingPayloadQueue.Enqueue(new IncomingDataSnapshot(dgramUid, senderIpEndpoint, payload, protocolPrefix));
                    }
                    break;

                case DatagramType.Ack:
                    {
                        // Ack is always being received via unreliable protocol with prefix 0, meaning that we should access the original prefix.
                        // Look at SendAckTo_Internal() for clarifications.
                        var originalProtocolPrefix = data[protocolOffset];

                        // Release pending packets.
                        _dgramStatePerEndPoint.TreReleasePendingOutgoingDgram(senderIpEndpoint, dgramUid, originalProtocolPrefix);
                    }
                    break;

                default:
                    FailedToProcessDgram(senderIpEndpoint);
                    break;
            }
        }

        private void SendPayloadTo_Internal(IPEndPoint endPoint, in ArraySegment<byte> data, uint dgramUid, byte protocolPrefix)
        {
            var protocolHeaderCount = PrepareOutgoingBufferProtocolHeader(_outgoingBuffer, DatagramType.Payload, _protocolKey, protocolPrefix, dgramUid);
            Buffer.BlockCopy(src: data.Array!, srcOffset: data.Offset, dst: _outgoingBuffer, dstOffset: protocolHeaderCount, count: data.Count);

            var dataSegment = new ArraySegment<byte>(_outgoingBuffer, offset: 0, count: protocolHeaderCount + data.Count);
            _dataTransferLayerPipeline?.ProcessOutgoingData(endPoint, ref dataSegment);
            UdpUtils.SendMtu(_socket, endPoint, data: dataSegment);
        }

        private void SendAckTo_Internal(IPEndPoint endPoint, uint dgramUid, byte originalProtocolPrefix)
        {
            var protocolHeaderCount = PrepareOutgoingBufferProtocolHeader(_outgoingBuffer, DatagramType.Ack, _protocolKey, protocolPrefix: 0, dgramUid);
            _outgoingBuffer[protocolHeaderCount++] = originalProtocolPrefix;

            var dataSegment = new ArraySegment<byte>(_outgoingBuffer, offset: 0, count: protocolHeaderCount);
            _dataTransferLayerPipeline?.ProcessOutgoingData(endPoint, ref dataSegment);
            UdpUtils.SendMtu(_socket, endPoint, data: dataSegment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlacklisted(IPAddress address) =>
            _ipAddressBlacklist != null && _ipAddressBlacklist.IsBlacklisted(address);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdateAddressInBlacklist(IPAddress address, int millisecondsToAdd)
        {
            if (millisecondsToAdd < 1)
                return;

            _ipAddressBlacklist ??= new IpAddressBlacklist();
            _ipAddressBlacklist.AddOrUpdateBlacklistTime(address, millisecondsToAdd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearEndpointData(IPEndPoint endPoint) =>
            _dgramStatePerEndPoint.ClearEndpointData(endPoint);

        public void Dispose()
        {
            UdpUtils.CloseSocket(_socket);

            _incomingPayloadQueue.Clear();
            _dgramStatePerEndPoint.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PrepareOutgoingBufferProtocolHeader(byte[] buffer, DatagramType datagramType, ushort protocolKey, byte protocolPrefix, uint dgramUid)
        {
            var protocolOffset = 0;
            var protocolKeyAsBytes = default(UShortByteUnion);
            protocolKeyAsBytes.UShort = protocolKey;

            buffer[protocolOffset++] = protocolKeyAsBytes.Byte0;
            buffer[protocolOffset++] = protocolKeyAsBytes.Byte1;

            buffer[protocolOffset++] = (byte)datagramType;
            buffer[protocolOffset++] = protocolPrefix;

            var dgramUidAsBytes = default(UIntByteUnion);
            dgramUidAsBytes.UInt = dgramUid;

            buffer[protocolOffset++] = dgramUidAsBytes.Byte0;
            buffer[protocolOffset++] = dgramUidAsBytes.Byte1;
            buffer[protocolOffset++] = dgramUidAsBytes.Byte2;
            buffer[protocolOffset++] = dgramUidAsBytes.Byte3;

            return protocolOffset;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UIntByteUnion
        {
            [FieldOffset(0)] public byte Byte0;
            [FieldOffset(1)] public byte Byte1;
            [FieldOffset(2)] public byte Byte2;
            [FieldOffset(3)] public byte Byte3;

            [FieldOffset(0)] public uint UInt;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UShortByteUnion
        {
            [FieldOffset(0)] public byte Byte0;
            [FieldOffset(1)] public byte Byte1;

            [FieldOffset(0)] public ushort UShort;
        }
    }
}