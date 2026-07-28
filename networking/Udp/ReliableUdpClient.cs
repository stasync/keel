using Core.Networking.Udp.LowLevel;
using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp
{
    /// <summary>
    ///  A high level client based on <see cref="UdpFullProtocol"/>.
    /// </summary>
    public sealed class ReliableUdpClient
    {
        private class ConnectionState
        {
            public readonly IPEndPoint ConnectionEndPoint;
            public readonly uint ConnectionUid;
            public readonly uint ValidationUid;
            public readonly byte Slot;
            public DateTime LastHeartbeatReceiveTime { get; internal set; }
            public DateTime LastHeatBeatSentTime { get; internal set; }

            public ConnectionState(IPEndPoint connectionEndPoint, uint connectionUid, uint validationUid, byte slot)
            {
                ConnectionEndPoint = connectionEndPoint;
                ConnectionUid = connectionUid;
                ValidationUid = validationUid;
                Slot = slot;
            }
        }

        public enum ServerMessageCodes : byte
        {
            ConnectConfirmed,
            ConnectionRejected,
            Heartbeat,
            Data,
            Disconnect
        }

        public enum RejectionReason : byte
        {
            ServerIsFull,
            ValidationFailure
        }

        public const uint HEARTBEAT_SEND_TIMEOUT_MS = 100;

        public event Action Connected = delegate { };
        public event Action<RejectionReason> ConnectionRejected = delegate { };
        public event Action ConnectionFailed = delegate { };
        public event Action Disconnected = delegate { };
        public event Action ReliabilityFailure = delegate { };
        public event Action<byte[], UdpFullProtocol.DgramDeliveryMethod> DataReceived = delegate { };
        public event Action HeartbeatSent = delegate { };

        public bool IsConnected => _connectionState != null;
        public uint ConnectionUid => IsConnected ? _connectionState.ConnectionUid : 0;
        public uint ValidationUid => IsConnected ? _connectionState.ValidationUid : 0;

        private UdpFullProtocol _protocol;
        private ConnectionState _connectionState;
        private readonly NetWriter _dataWriter = new();
        private readonly NetReader _dataReader = new();
        private DateTime _connectionStartTime;

        public void Connect(IPEndPoint endPoint, string connectionData = null)
        {
            if (_connectionState != null)
            {
                // Already connected.
                Core.Utils.Debug.Logger.LogError("Already connected.");
                return;
            }

            if (_protocol != null)
            {
                Core.Utils.Debug.Logger.LogError("Already connecting.");
                return;
            }

            _connectionState = null;
            _protocol = new UdpFullProtocol(port: 0, protocolKey: 0);

            _dataWriter.SeekZero();
            _dataWriter.WriteByte((byte)ReliableUdpListener.ClientMessageCodes.Connect);
            _dataWriter.WriteString(connectionData);
            _protocol.SendTo(endPoint, _dataWriter.AsArraySegment(), UdpFullProtocol.DgramDeliveryMethod.Reliable);
            _connectionStartTime = DateTime.UtcNow;
        }

        public void Send(ArraySegment<byte> data, UdpFullProtocol.DgramDeliveryMethod dgramDeliveryMethod)
        {
            EnsureValid();

            if (!IsConnected)
            {
                Core.Utils.Debug.Logger.LogError("Unable to send data: not connected yet.");
                return;
            }

            _dataWriter.SeekZero();
            _dataWriter.WriteByte((byte)ReliableUdpListener.ClientMessageCodes.Data);
            _dataWriter.WritePackedUInt32(_connectionState.ConnectionUid);
            _dataWriter.WritePackedUInt32(_connectionState.ValidationUid);
            _dataWriter.WriteByte(_connectionState.Slot);
            _dataWriter.WriteBytesAndSize(data);
            _protocol.SendTo(_connectionState.ConnectionEndPoint, _dataWriter.AsArraySegment(), dgramDeliveryMethod);
        }

        public void Update()
        {
            if (_protocol == null)
                return;

            var currentReliabilityFailureCount = _protocol.ReliabilityFailureCount;
            _protocol.Poll();
            if (currentReliabilityFailureCount < _protocol.ReliabilityFailureCount)
                ReliabilityFailure();

            while (_protocol != null && _protocol.TryDequeueIncoming(out var incomingDataSnapshot))
                ProcessData(incomingDataSnapshot);

            if (IsConnected)
            {
                TrySendHeartbeat();
                UpdateHeartbeat();
            }
            else
            {
                if ((DateTime.UtcNow - _connectionStartTime).TotalMilliseconds > ReliableUdpListener.HEARTBEAT_TIMEOUT_MS)
                {
                    ConnectionFailed();
                    Disconnect();
                }
            }
        }

        private void TrySendHeartbeat()
        {
            // Send 10 times per second.
            if ((DateTime.UtcNow - _connectionState.LastHeatBeatSentTime).TotalMilliseconds < HEARTBEAT_SEND_TIMEOUT_MS)
                return;

            _connectionState.LastHeatBeatSentTime = DateTime.UtcNow;

            _dataWriter.SeekZero();
            _dataWriter.WriteByte((byte)ReliableUdpListener.ClientMessageCodes.Heartbeat);
            _dataWriter.WritePackedUInt32(_connectionState.ConnectionUid);
            _dataWriter.WritePackedUInt32(_connectionState.ValidationUid);
            _dataWriter.WriteByte(_connectionState.Slot);

            // NOTE: heartbeats are deliberately unreliable. A reliable one is resent on every Poll tick until
            // it is acked, and every copy is acked in turn, so the cost of a fixed heartbeat rate would scale
            // with latency. A lost heartbeat needs no recovery - the next one follows in
            // HEARTBEAT_SEND_TIMEOUT_MS, and HEARTBEAT_TIMEOUT_MS tolerates roughly 20 consecutive losses.
            _protocol.SendTo(_connectionState.ConnectionEndPoint, _dataWriter.AsArraySegment(), UdpFullProtocol.DgramDeliveryMethod.Unreliable);

            HeartbeatSent();
        }

        private void UpdateHeartbeat()
        {
            var currentDelta = DateTime.UtcNow - _connectionState.LastHeartbeatReceiveTime;
            if (currentDelta.TotalMilliseconds < ReliableUdpListener.HEARTBEAT_TIMEOUT_MS)
                return;

            Core.Utils.Debug.Logger.LogError($"[{GetType().FullName}] UpdateHeartbeat error: {currentDelta.TotalMilliseconds}ms");
            Disconnect();
        }

        private void ProcessData(in IncomingDataSnapshot incomingDataSnapshot)
        {
            // As we process incoming data, we should always be ready that it might in some incorrect format.
            // NOTE: a malformed datagram must never escape Update(), otherwise the client stops updating
            // entirely: it stops heartbeating, never runs its own timeout check, logs nothing, and the
            // server drops it once HEARTBEAT_TIMEOUT_MS elapses.
            try
            {
                ProcessServerMessage(in incomingDataSnapshot);
            }
            catch (Exception e)
            {
                // Discard the offending datagram and keep the update loop alive.
                Core.Utils.Debug.Logger.LogError($"[{GetType().FullName}] MALFORMED DATAGRAM from '{incomingDataSnapshot.EndPoint}' discarded: {e.Message}");
            }
        }

        private void ProcessServerMessage(in IncomingDataSnapshot incomingDataSnapshot)
        {
            _dataReader.Replace(incomingDataSnapshot.Buffer);

            var messageCode = (ServerMessageCodes)_dataReader.ReadByte();
            switch (messageCode)
            {
                case ServerMessageCodes.ConnectConfirmed:
                    var connectionUid = _dataReader.ReadPackedUInt32();
                    var validationUid = _dataReader.ReadPackedUInt32();
                    var slot = _dataReader.ReadByte();
                    _connectionState = new ConnectionState(incomingDataSnapshot.EndPoint, connectionUid, validationUid, slot)
                    {
                        LastHeartbeatReceiveTime = DateTime.UtcNow,
                        LastHeatBeatSentTime = DateTime.UtcNow
                    };

                    Connected();
                    break;

                case ServerMessageCodes.ConnectionRejected:
                    var rejectionReason = (RejectionReason)_dataReader.ReadByte();
                    ConnectionRejected(rejectionReason);
                    Disconnect();
                    break;

                case ServerMessageCodes.Heartbeat:
                    // Add the connection for entry point if it not exists.
                    if (_connectionState != null)
                        _connectionState.LastHeartbeatReceiveTime = DateTime.UtcNow;

                    break;

                case ServerMessageCodes.Data:
                    if (_connectionState != null)
                    {
                        var payload = _dataReader.ReadBytesAndSize();
                        DataReceived(payload, (UdpFullProtocol.DgramDeliveryMethod)incomingDataSnapshot.ProtocolPrefix);
                    }

                    break;

                case ServerMessageCodes.Disconnect:
                    Disconnect();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(messageCode), messageCode, "Unknown server message code");
            }
        }

        public void RegisterIncomingPacketSimulator(UdpReliableProtocol.IncomingPacketSimulator value)
        {
            EnsureValid();
            _protocol.RegisterIncomingPacketSimulator(value);
        }

        public void UnregisterIncomingPacketSimulator(UdpReliableProtocol.IncomingPacketSimulator value)
        {
            EnsureValid();
            _protocol.UnregisterIncomingPacketSimulator(value);
        }

        public void UnregisterAllIncomingPacketSimulators()
        {
            EnsureValid();
            _protocol.UnregisterAllIncomingPacketSimulators();
        }

        public void RegisterDataTransferLayer(UdpReliableProtocol.DataTransferLayer value)
        {
            EnsureValid();
            _protocol.RegisterDataTransferLayer(value);
        }

        public void UnregisterDataTransferLayer(UdpReliableProtocol.DataTransferLayer value)
        {
            EnsureValid();
            _protocol.UnregisterDataTransferLayer(value);
        }

        public void UnregisterAllDataTransferLayers()
        {
            EnsureValid();
            _protocol.UnregisterAllDataTransferLayers();
        }

        public void Disconnect()
        {
            if (_protocol == null)
                return;

            _protocol.Dispose();
            _protocol = null;

            _connectionState = null;
            Disconnected();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureValid()
        {
            if (_protocol == null)
                throw new InvalidOperationException(GetType().FullName);
        }
    }
}