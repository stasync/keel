using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace Core.Networking.Udp.LowLevel
{
    public sealed partial class UdpReliableProtocol
    {
        /// <summary>
        /// Abstract class to implement custom incoming packet simulators.
        /// </summary>
        public abstract class IncomingPacketSimulator
        {
            public abstract void Update(in List<(byte[], IPEndPoint)> incomingQueue);
        }

        /// <summary>
        /// A helper wrapper to be able to simulate incoming packets.
        /// Runs at the lowest level of <see cref="UdpReliableProtocol"/> to simulate more accurate.
        /// </summary>
        private sealed class IncomingPacketSimulationPipeline
        {
            internal int SimulatorsCount => _incomingPacketSimulators.Count;

            private readonly List<(byte[], IPEndPoint)> _incomingDataQueue = new();
            private readonly List<IncomingPacketSimulator> _incomingPacketSimulators = new();

            /// <summary>
            /// A date we get from the network interface.
            /// </summary>
            internal void OnDataReceived(byte[] packetData, IPEndPoint sender) =>
                _incomingDataQueue.Add((packetData, sender));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Update()
            {
                // Update all simulators.
                foreach (var simulator in _incomingPacketSimulators)
                    simulator.Update(_incomingDataQueue);
            }

            internal bool TryDequeuePacket(out (byte[] data, IPEndPoint sender) result)
            {
                result = default;
                if (_incomingDataQueue.Count == 0)
                    return false;

                result = _incomingDataQueue[0];
                _incomingDataQueue.RemoveAt(index: 0);
                return true;
            }

            internal void RegisterSimulator(IncomingPacketSimulator value)
            {
                if (value != null)
                    _incomingPacketSimulators.Add(value);
            }

            internal void UnregisterSimulator(IncomingPacketSimulator value) =>
                _incomingPacketSimulators.Remove(value);

            internal void UnregisterAllSimulators() =>
                _incomingPacketSimulators.Clear();
        }

        public void RegisterIncomingPacketSimulator(IncomingPacketSimulator value)
        {
            _incomingPacketSimulationPipeline ??= new IncomingPacketSimulationPipeline();
            _incomingPacketSimulationPipeline.RegisterSimulator(value);
        }

        public void UnregisterIncomingPacketSimulator(IncomingPacketSimulator value)
        {
            if (_incomingPacketSimulationPipeline == null)
                return;

            _incomingPacketSimulationPipeline.UnregisterSimulator(value);
            if (_incomingPacketSimulationPipeline.SimulatorsCount == 0)
                _incomingPacketSimulationPipeline = null;
        }

        public void UnregisterAllIncomingPacketSimulators()
        {
            _incomingPacketSimulationPipeline?.UnregisterAllSimulators();
            _incomingPacketSimulationPipeline = null;
        }
    }
}