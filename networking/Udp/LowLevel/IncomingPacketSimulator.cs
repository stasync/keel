using System.Collections.Generic;
using System.Net;

namespace Keel.Networking.Udp.LowLevel
{
    /// <summary>
    /// Abstract class to implement custom incoming packet simulators.
    /// </summary>
    public abstract class IncomingPacketSimulator
    {
        public abstract void Update(in List<(byte[], IPEndPoint)> incomingQueue);
    }
}