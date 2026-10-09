using System;

namespace Keel.DependencyInjection.Events
{
    [DefaultImplementation(typeof(Broadcaster))]
    public interface IBroadcaster
    {
        IReadOnlyChannelEventCollection ChannelEventCollection { get; }

        void RegisterObject(object targetObject);
        void UnregisterObject(object targetObject);

        void AddListener<T>(Action<T> listener) where T : struct;
        void AddListener<T>(int channel, Action<T> listener) where T : struct;

        void RemoveListener<T>(Action<T> listener) where T : struct;
        void RemoveListener<T>(int channel, Action<T> listener) where T : struct;

        bool Invoke<T>(T eventData, bool requireReceiver = false) where T : struct;
        bool Invoke<T>(int channel, T eventData, bool requireReceiver = false) where T : struct;

        void Clear();
    }
}