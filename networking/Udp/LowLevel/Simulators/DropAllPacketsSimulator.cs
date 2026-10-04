using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Simulators
{
    /// <summary>
    /// All packets will be lost, if this simulator is registered.
    /// </summary>
    public sealed class DropAllPacketsSimulator : UdpReliableProtocol.IncomingPacketSimulator
    {
        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue) =>
            incomingQueue.Clear();
    }
}