using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel.Simulators
{
    /// <summary>
    /// A simulator example - does nothing.
    /// </summary>
    public sealed class DummySimulator : IncomingPacketSimulator
    {
        public override void Update(in List<(byte[], IPEndPoint)> incomingQueue)
        {
        }
    }
}