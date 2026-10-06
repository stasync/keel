using System;
using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Simulators
{
    public sealed class RandomPacketDuplicationSimulator : IncomingPacketSimulator
    {
        /// <summary>
        /// A chance of every packet to be duplicated.
        /// A duplication chance, should be in [0, 100] range.
        /// </summary>
        public float PacketDuplicationChancePercent
        {
            get => _duplicationChanceNormalizedValue * 100f;
            set => _duplicationChanceNormalizedValue = Math.Clamp(value, 0f, 100f) / 100f;
        }

        private float _duplicationChanceNormalizedValue = 0.1f;
        private readonly Random _random = new();

        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
            // Most simple way to address this is a reverse loop.
            for (var index = incomingQueue.Count - 1; index >= 0; index--)
            {
                if (_random.NextDouble() <= _duplicationChanceNormalizedValue)
                    incomingQueue.Insert(index, incomingQueue[index]);
            }
        }
    }
}