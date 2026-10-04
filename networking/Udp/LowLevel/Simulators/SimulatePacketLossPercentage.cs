using System;
using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Simulators
{
    public sealed class SimulatePacketLossPercentage : UdpReliableProtocol.IncomingPacketSimulator
    {
        /// <summary>
        /// A packet loss percentage, should be in [0, 100] range.
        /// For example:
        /// 0.2 - every 500 packet will be dropped.
        /// 5 - every 20 packet will be dropped.
        /// 10 - every 10 packet will be dropped.
        /// 50 - every 2 packet will be dropped.
        /// </summary>
        public float PacketLossPercent { get; set; } = 10f;

        private uint _packetCounter;
        private readonly HashSet<int> _packetToRemoveIndexes = new();

        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
            _packetToRemoveIndexes.Clear();
            if (PacketLossPercent < 0.001f)
            {
                _packetCounter += (uint)incomingQueue.Count;
                return;
            }

            var percentageFactor = (int)Math.Round(100f / Math.Min(PacketLossPercent, 100f));
            for (var index = 0; index < incomingQueue.Count; index++)
            {
                _packetCounter++;
                if (_packetCounter % percentageFactor == 0)
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