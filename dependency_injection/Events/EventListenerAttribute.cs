using System;

namespace Keel.DependencyInjection.Events
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EventListenerAttribute : Attribute
    {
        public readonly ushort EventCode;
        public readonly int Channel;

        public EventListenerAttribute(ushort eventCode, int channel = 0)
        {
            EventCode = eventCode;
            Channel = channel;
        }
    }
}