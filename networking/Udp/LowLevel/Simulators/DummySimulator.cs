using System.Collections.Generic;
using System.Net;

namespace Core.Networking.Udp.LowLevel.Simulators
{
    /// <summary>
    /// A simulator example - does nothing.
    /// </summary>
    public sealed class DummySimulator : UdpReliableProtocol.IncomingPacketSimulator
    {
        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
        }
    }
}