using System;

namespace Keel.DependencyInjection.Events
{
    [AttributeUsage(AttributeTargets.Method)]
    public class EventListenerAttribute : Attribute
    {
        public readonly int Channel;

        public EventListenerAttribute(int channel = 0) =>
            Channel = channel;
    }
}