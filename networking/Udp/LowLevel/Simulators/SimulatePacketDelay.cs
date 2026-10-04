using System;
using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Simulators
{
    public sealed class SimulatePacketDelay : UdpReliableProtocol.IncomingPacketSimulator
    {
        /// <summary>
        /// Desired packet delay in milliseconds.
        /// </summary>
        public float PacketMinDelayMs
        {
            get => _packetMinDelayMs;
            set => _packetMinDelayMs = Math.Clamp(value, 0f, PacketMaxDelayMs);
        }
        private float _packetMinDelayMs = 100f;

        public float PacketMaxDelayMs
        {
            get => _packetMaxDelayMs;
            set => _packetMaxDelayMs = Math.Max(value, PacketMinDelayMs);
        }
        private float _packetMaxDelayMs = 200f;

        public bool ReverseDelayedPackets { get; set; }

        private readonly Random _random = new();

        private readonly List<DelayedPacket> _delayedPacketQueue = new();
        private readonly List<DelayedPacket> _packetsToForward = new();

        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
            foreach ((var packetData, var sender) in incomingQueue)
            {
                var delay = _packetMinDelayMs + (_packetMaxDelayMs - _packetMinDelayMs) * _random.NextDouble();
                var delayedPacket = new DelayedPacket(packetData, sender, (float)delay);

                if (ReverseDelayedPackets)
                    _delayedPacketQueue.Insert(index: 0, delayedPacket);
                else
                    _delayedPacketQueue.Add(delayedPacket);
            }

            // Update delayed packets lifetime.
            _packetsToForward.Clear();
            for (var i = _delayedPacketQueue.Count - 1; i >= 0; i--)
            {
                var delayedPacket = _delayedPacketQueue[i];
                var currentLifetime = (DateTime.UtcNow - delayedPacket.RecordTime).TotalMilliseconds;
                if (currentLifetime < delayedPacket.TotalLifetimeMs)
                    continue;

                // Inserting to 1st element as we are doing reverse loop.
                _packetsToForward.Insert(index: 0, delayedPacket);
                _delayedPacketQueue.RemoveAt(i);
            }

            // Forward the packets to the incoming queue.
            incomingQueue.Clear();
            foreach (var packetToForward in _packetsToForward)
                incomingQueue.Add((packetToForward.PacketData, packetToForward.Sender));
        }

        private struct DelayedPacket
        {
            internal readonly byte[] PacketData;
            internal readonly IPEndPoint Sender;
            internal readonly float TotalLifetimeMs;
            internal readonly DateTime RecordTime;

            internal DelayedPacket(byte[] packetData, IPEndPoint sender, float totalLifetimeMs)
            {
                PacketData = packetData;
                Sender = sender;
                TotalLifetimeMs = totalLifetimeMs;
                RecordTime = DateTime.UtcNow;
            }
        }
    }
}