using System;

namespace Core.DependencyInjection.Events
{
    [DefaultImplementation(typeof(Broadcaster))]
    public interface IBroadcaster
    {
        IReadOnlyChannelEventCollection ChannelEventCollection { get; }

        void RegisterObject(object targetObject);
        void UnregisterObject(object targetObject);

        void AddListener(ushort eventCode, Action<object[]> listener);
        void AddListener(int channel, ushort eventCode, Action<object[]> listener);

        void RemoveListener(ushort eventCode, Action<object[]> listener);
        void RemoveListener(int channel, ushort eventCode, Action<object[]> listener);

        bool Invoke(ushort eventCode, bool requireReceiver, params object[] args);
        bool Invoke(int channel, ushort eventCode, bool requireReceiver, params object[] args);

        void Clear();
    }
}