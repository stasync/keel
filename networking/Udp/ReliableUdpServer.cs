using Keel.Networking.Udp.LowLevel;
using Keel.Utils;
using Keel.Utils.Debug;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Keel.Networking.Udp
{
    /// <summary>
    /// A high level server based on <see cref="UdpProtocol"/>.
    /// </summary>
    public sealed class ReliableUdpServer : IDisposable
    {
        public delegate bool ValidateConnectionDelegate(in ConnectionRequest connectionRequest);

        public struct ConnectionRequest
        {
            public readonly string ConnectionData;
            public readonly IPEndPoint EndPoint;

            internal ConnectionRequest(string connectionData, IPEndPoint endPoint)
            {
                ConnectionData = connectionData;
                EndPoint = endPoint;
            }
        }

        public enum ClientMessageCodes : byte
        {
            Connect,
            Heartbeat,
            Data
        }

        private sealed class Connection
        {
            internal readonly uint Uid;
            internal readonly uint ValidationUid;

            /// <summary>
            /// Where outgoing data is sent. Pinned at connection - see ReportEndPointChange.
            /// </summary>
            internal IPEndPoint EndPoint { get; set; }

            internal DateTime LastHeartbeatTime { get; set; }

            internal Connection(uint uid, uint validationUid)
            {
                Uid = uid;
                ValidationUid = validationUid;
            }
        }

        /// <summary>
        /// Derived from <see cref="UdpTransport.MAX_RESEND_DURATION_MS"/> so the two horizons stay
        /// in step: there is no point declaring a connection dead while the layer below is still retransmitting
        /// for it, nor retransmitting for a connection that is already gone.
        /// </summary>
        public const uint HEARTBEAT_TIMEOUT_MS = UdpTransport.MAX_RESEND_DURATION_MS;

        public event Action<uint, ConnectionRequest> Connected = delegate { };
        public event Action<uint> Disconnected = delegate { };
        public event Action<uint, byte[], UdpProtocol.DeliveryMethod> DataReceived = delegate { };
        public event Action<IPEndPoint, ReliableUdpClient.RejectionReason> ConnectionRejected = delegate { };
        public event Action<IPEndPoint, string> UnexpectedClientAction = delegate { };
        public event ValidateConnectionDelegate ValidateConnection;

        public readonly int Port;

        private readonly UidProvider _connectionUidProvider = new();
        private readonly UdpProtocol _protocol;
        private readonly Queue<ConnectionRequest> _connectionRequests = new();
        private readonly Connection[] _connectionSlots;
        private readonly NetWriter _dataWriter = new();
        private readonly NetReader _dataReader = new();
        private readonly Dictionary<uint, DateTime> _recentDisconnectsLookup = new();

        private DateTime _nextRecentDisconnectsPurgeTime;

        public ReliableUdpServer(int maxConnections, int port, ushort protocolKey)
        {
            Port = port;
            if (Port <= 0)
                Port = Utils.GetAvailableUdpPort();

            // 0 may be used to test rejections.
            maxConnections = Math.Clamp(maxConnections, min: 0, max: byte.MaxValue);

            _connectionSlots = new Connection[maxConnections];
            _protocol = new UdpProtocol(Port, protocolKey: protocolKey);
            _protocol.FailedToProcessDatagram += OnFailedToProcessDatagram;
        }

        public void Update()
        {
            if (_protocol == null)
                return;

            var currentReliabilityFailureCount = _protocol.ReliabilityFailureCount;
            _protocol.Poll();
            if (currentReliabilityFailureCount < _protocol.ReliabilityFailureCount)
            {
                // TODO: Handle reliability failures.
                // TODO: Probably listener shouldn't do anything about it.
            }

            while (_protocol.TryDequeueIncoming(out var incomingDataSnapshot))
                ProcessData(incomingDataSnapshot);

            while (_connectionRequests.Count > 0)
            {
                var newConnectionRequest = _connectionRequests.Dequeue();
                var targetConnectionSlot = -1;

                // TODO: Probably the first thing we should check is whether any slot is available.
                var isNewConnectionValid = ValidateConnection == null || ValidateConnection(newConnectionRequest);
                if (isNewConnectionValid)
                {
                    for (var slotIndex = 0; slotIndex < _connectionSlots.Length; slotIndex++)
                    {
                        var connection = _connectionSlots[slotIndex];

                        // Check if busy.
                        if (connection != null)
                            continue;

                        targetConnectionSlot = slotIndex;
                        break;
                    }
                }

                _dataWriter.SeekZero();
                if (targetConnectionSlot >= 0)
                {
                    var uid = _connectionUidProvider.Next();
                    var validationUid = (uint)RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
                    var newConnectionInstance = new Connection(uid, validationUid)
                    {
                        EndPoint = newConnectionRequest.EndPoint,
                        LastHeartbeatTime = DateTime.UtcNow
                    };

                    // Respond to a client and register a connection.
                    _connectionSlots[targetConnectionSlot] = newConnectionInstance;

                    Connected(newConnectionInstance.Uid, newConnectionRequest);

                    _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.ConnectConfirmed);
                    _dataWriter.WritePackedUInt32(newConnectionInstance.Uid);
                    _dataWriter.WritePackedUInt32(newConnectionInstance.ValidationUid);
                    _dataWriter.WriteByte((byte)targetConnectionSlot);

                    _protocol.SendTo(newConnectionRequest.EndPoint, _dataWriter.AsArraySegment(), UdpProtocol.DeliveryMethod.Reliable);
                }
                else
                {
                    var rejectionReason = isNewConnectionValid ? ReliableUdpClient.RejectionReason.ServerIsFull : ReliableUdpClient.RejectionReason.ValidationFailure;

                    ConnectionRejected(newConnectionRequest.EndPoint, rejectionReason);

                    // Send unreliable response.
                    // NOTE: Rejection response should always be unreliable, as we want to clean up endpoint information after.
                    // NOTE: Client should not rely on the rejection message completely.
                    _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.ConnectionRejected);
                    _dataWriter.WriteByte((byte)rejectionReason);
                    _protocol.SendTo(newConnectionRequest.EndPoint, _dataWriter.AsArraySegment(), UdpProtocol.DeliveryMethod.Unreliable);

                    // Connection rejected - lets also clean up endpoint data.
                    _protocol.RemoveEndPointData(newConnectionRequest.EndPoint);
                }
            }

            UpdateConnectionHeartbeats();
            PurgeRecentDisconnects();
        }

        public bool HasConnection(uint uid)
        {
            EnsureValid();

            foreach (var connection in _connectionSlots)
            {
                if (connection != null && connection.Uid == uid)
                    return true;
            }

            return false;
        }

        public bool TryGetConnectionEndPoint(uint uid, out IPEndPoint endPoint)
        {
            EnsureValid();

            endPoint = null;
            foreach (var connection in _connectionSlots)
            {
                if (connection == null || connection.Uid != uid)
                    continue;

                endPoint = connection.EndPoint;
                return true;
            }

            return false;
        }

        private void ProcessData(in IncomingDataSnapshot incomingDataSnapshot)
        {
            // As we process incoming data, we should always be ready that it might be in an incorrect format.
            try
            {
                _dataReader.Replace(incomingDataSnapshot.Buffer);
                var messageCode = (ClientMessageCodes)_dataReader.ReadByte();

                switch (messageCode)
                {
                    case ClientMessageCodes.Connect:
                        {
                            var connectionData = _dataReader.ReadString();
                            _connectionRequests.Enqueue(new ConnectionRequest(connectionData, incomingDataSnapshot.EndPoint));
                        }
                        break;

                    case ClientMessageCodes.Heartbeat:
                        {
                            var connectionUid = _dataReader.ReadPackedUInt32();
                            var validationUid = _dataReader.ReadPackedUInt32();
                            var connectionSlot = _dataReader.ReadByte();

                            var connection = _connectionSlots[connectionSlot];
                            if (connection == null)
                            {
                                if (!_recentDisconnectsLookup.ContainsKey(connectionUid))
                                    UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connectionUid}' is not yet registered: '{messageCode}'");

                                break;
                            }

                            if (connection.Uid != connectionUid || connection.ValidationUid != validationUid)
                            {
                                UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connectionUid}' doesn't match the slot '{connectionSlot}': '{messageCode}'");
                                break;
                            }

                            if (!incomingDataSnapshot.EndPoint.Equals(connection.EndPoint))
                            {
                                UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connection.Uid}' is now arriving from '{incomingDataSnapshot.EndPoint}' but stays pinned to '{connection.EndPoint}'. Outgoing data keeps targeting the pinned address, so this client may stop receiving while still appearing connected");
                                break;
                            }

                            // Update heartbeat.
                            connection.LastHeartbeatTime = DateTime.UtcNow;

                            // Add the connection for entry point if it not exists.
                            _dataWriter.SeekZero();
                            _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.Heartbeat);

                            // NOTE: unreliable for the same reason as the client side heartbeat - see TrySendHeartbeat.
                            _protocol.SendTo(incomingDataSnapshot.EndPoint, _dataWriter.AsArraySegment(), UdpProtocol.DeliveryMethod.Unreliable);
                        }
                        break;

                    case ClientMessageCodes.Data:
                        {
                            var connectionUid = _dataReader.ReadPackedUInt32();
                            var validationUid = _dataReader.ReadPackedUInt32();
                            var connectionSlot = _dataReader.ReadByte();

                            var connection = _connectionSlots[connectionSlot];
                            if (connection == null)
                            {
                                if (!_recentDisconnectsLookup.ContainsKey(connectionUid))
                                    UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connectionUid}' is not yet registered: '{messageCode}'");

                                break;
                            }

                            if (connection.Uid != connectionUid || connection.ValidationUid != validationUid)
                            {
                                UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connectionUid}' doesn't match the slot '{connectionSlot}': '{messageCode}'");
                                break;
                            }

                            if (!incomingDataSnapshot.EndPoint.Equals(connection.EndPoint))
                            {
                                UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected data - connection '{connection.Uid}' is now arriving from '{incomingDataSnapshot.EndPoint}' but stays pinned to '{connection.EndPoint}'. Outgoing data keeps targeting the pinned address, so this client may stop receiving while still appearing connected");
                                break;
                            }

                            var payload = _dataReader.ReadBytesAndSize();
                            DataReceived(connection.Uid, payload, (UdpProtocol.DeliveryMethod)incomingDataSnapshot.Channel);
                        }
                        break;

                    default:
                        UnexpectedClientAction(incomingDataSnapshot.EndPoint, $"Unexpected message code: '{messageCode}'");
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                UnexpectedClientAction(incomingDataSnapshot.EndPoint, e.Message);
            }
        }

        private void UpdateConnectionHeartbeats()
        {
            for (var i = 0; i < _connectionSlots.Length; i++)
            {
                var connection = _connectionSlots[i];
                if (connection == null)
                    continue;

                var heartBeatDelta = DateTime.UtcNow - connection.LastHeartbeatTime;
                if (heartBeatDelta.TotalMilliseconds < HEARTBEAT_TIMEOUT_MS)
                    continue;

                // Unregister endpoint data.
                _protocol.RemoveEndPointData(connection.EndPoint);

                // Nothing to do, just drop.
                _connectionSlots[i] = null;
                _recentDisconnectsLookup[connection.Uid] = DateTime.UtcNow;

                Logger.LogInfo($"[{GetType().FullName}] Connection '{connection.Uid}' disconnected due to heartbeat timeout ({heartBeatDelta.TotalMilliseconds}ms).");

                // Callback.
                Disconnected(connection.Uid);
            }
        }

        /// <summary>
        /// Drops the recently disconnected uids that are old enough that a client cannot still be sending against them.
        ///
        /// NOTE: releasing per endpoint protocol state is not done here - <see cref="UdpProtocol"/> expires
        /// its own tracking, since it is what allocates it.
        /// </summary>
        private void PurgeRecentDisconnects()
        {
            if (DateTime.UtcNow < _nextRecentDisconnectsPurgeTime)
                return;

            _nextRecentDisconnectsPurgeTime = DateTime.UtcNow.AddMilliseconds(HEARTBEAT_TIMEOUT_MS);

            foreach (var connectionId in new List<uint>(_recentDisconnectsLookup.Keys))
            {
                if (!_recentDisconnectsLookup.TryGetValue(connectionId, out var disconnectTime))
                    continue;

                var delta = (DateTime.UtcNow - disconnectTime).TotalMilliseconds;
                if (delta > HEARTBEAT_TIMEOUT_MS)
                    _recentDisconnectsLookup.Remove(connectionId);
            }
        }

        private void OnFailedToProcessDatagram(IPEndPoint sender)
        {
            // NOTE: this usually indicates that a client sends incorrect/corrupted messages.
            UnexpectedClientAction(sender, "Unexpected data received on the protocol level.");
        }

        public void SendToAll(ArraySegment<byte> data, UdpProtocol.DeliveryMethod deliveryMethod)
        {
            EnsureValid();

            _dataWriter.SeekZero();
            _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.Data);
            _dataWriter.WriteBytesAndSize(data);

            foreach (var connection in _connectionSlots)
            {
                if (connection != null)
                    _protocol.SendTo(connection.EndPoint, _dataWriter.AsArraySegment(), deliveryMethod);
            }
        }

        public void SendTo(uint targetConnectionUid, ArraySegment<byte> data, UdpProtocol.DeliveryMethod deliveryMethod)
        {
            EnsureValid();

            foreach (var connection in _connectionSlots)
            {
                if (connection == null || connection.Uid != targetConnectionUid)
                    continue;

                _dataWriter.SeekZero();
                _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.Data);
                _dataWriter.WriteBytesAndSize(data);
                _protocol.SendTo(connection.EndPoint, _dataWriter.AsArraySegment(), deliveryMethod);
                break;
            }
        }

        public void AddOrUpdateAddressInBlacklist(IPAddress address, int millisecondsToAdd, string reasonError = null)
        {
            EnsureValid();

            if (millisecondsToAdd < 1)
                return;

            var wasAlreadyBlacklisted = _protocol.IsBlacklisted(address);
            _protocol.AddOrUpdateAddressInBlacklist(address, millisecondsToAdd);

            // Only log this if it was not blacklisted before.
            if (!wasAlreadyBlacklisted && !string.IsNullOrWhiteSpace(reasonError))
                Logger.LogError($"[{GetType().FullName}] Sender address '{address}' will be added to blacklist for '{millisecondsToAdd}ms'. Reason: {reasonError}.");
        }

        public void AddAddressToBlacklistIfAbsent(IPAddress address, int millisecondsToAdd, string reasonError = null)
        {
            EnsureValid();

            if (millisecondsToAdd < 1)
                return;

            var alreadyBlacklisted = _protocol.IsBlacklisted(address);
            if (alreadyBlacklisted)
                return;

            _protocol.AddOrUpdateAddressInBlacklist(address, millisecondsToAdd);
            Logger.LogError($"[{GetType().FullName}] Sender address '{address}' will be added to blacklist for '{millisecondsToAdd}ms'. Reason: {reasonError}.");
        }

        public void RegisterIncomingPacketSimulator(IncomingPacketSimulator value)
        {
            EnsureValid();
            _protocol.RegisterIncomingPacketSimulator(value);
        }

        public void UnregisterIncomingPacketSimulator(IncomingPacketSimulator value)
        {
            EnsureValid();
            _protocol.UnregisterIncomingPacketSimulator(value);
        }

        public void UnregisterAllIncomingPacketSimulators()
        {
            EnsureValid();
            _protocol.UnregisterAllIncomingPacketSimulators();
        }

        public void RegisterDataTransferLayer(DataTransferLayer value)
        {
            EnsureValid();
            _protocol.RegisterDataTransferLayer(value);
        }

        public void UnregisterDataTransferLayer(DataTransferLayer value)
        {
            EnsureValid();
            _protocol.UnregisterDataTransferLayer(value);
        }

        public void UnregisterAllDataTransferLayers()
        {
            EnsureValid();
            _protocol.UnregisterAllDataTransferLayers();
        }

        public void Disconnect(uint uid)
        {
            EnsureValid();

            for (var i = 0; i < _connectionSlots.Length; i++)
            {
                var connection = _connectionSlots[i];
                if (connection == null || connection.Uid != uid)
                    continue;

                // Send a disconnect message.
                _dataWriter.SeekZero();
                _dataWriter.WriteByte((byte)ReliableUdpClient.ServerMessageCodes.Disconnect);
                _protocol.SendTo(connection.EndPoint, _dataWriter.AsArraySegment(), UdpProtocol.DeliveryMethod.Unreliable);

                // Unregister endpoint data.
                _protocol.RemoveEndPointData(connection.EndPoint);

                // Nothing to do, just drop.
                _connectionSlots[i] = null;
                _recentDisconnectsLookup[connection.Uid] = DateTime.UtcNow;
                Disconnected(connection.Uid);

                // Debug log.
                Logger.LogInfo($"[{GetType().FullName}] Disconnected: {uid}.");

                // Connection found - no need to continue.
                break;
            }
        }

        public void Dispose()
        {
            if (_protocol == null)
                return;

            _protocol.Dispose();
            _connectionRequests.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureValid()
        {
            if (_protocol == null)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}