using System;
using System.Collections.Generic;
using System.Net;

namespace Core.Networking.Udp.LowLevel.Simulators
{
    public sealed class SimulatePacketLossByChance : UdpReliableProtocol.IncomingPacketSimulator
    {
        /// <summary>
        /// A chance of every packet to be dropped.
        /// A drop chance, should be in [0, 100] range.
        /// </summary>
        public float PacketLossChancePercent
        {
            get => _dropChanceNormalizedValue * 100f;
            set => _dropChanceNormalizedValue = Math.Clamp(value, 0f, 100f) / 100f;
        }

        private float _dropChanceNormalizedValue = 0.1f;
        private readonly Random _random = new();
        private readonly HashSet<int> _packetToRemoveIndexes = new();

        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
            _packetToRemoveIndexes.Clear();
            for (var index = 0; index < incomingQueue.Count; index++)
            {
                if (_random.NextDouble() <= _dropChanceNormalizedValue)
                    _packetToRemoveIndexes.Add(index);
            }

            for (var packetIndex = incomingQueue.Count - 1; packetIndex >= 0; packetIndex--)
            {
                if (_packetToRemoveIndexes.Contains(packetIndex))
                    incomingQueue.RemoveAt(packetIndex);
            }
        }
    }
}