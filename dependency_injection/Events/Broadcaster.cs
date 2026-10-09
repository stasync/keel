using Keel.DependencyInjection.Events.Internal;
using Keel.Utils.Debug;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Keel.DependencyInjection.Events
{
    public class Broadcaster : IBroadcaster
    {
        private sealed class InternalChannelEventCollection : IReadOnlyChannelEventCollection
        {
            private readonly Dictionary<int, InternalEventCollection> _value = new();
            public int Count => _value.Count;

            public void AddListener(int channel, Type eventType, Delegate listener)
            {
                if (!_value.TryGetValue(channel, out var internalEventCollection))
                    _value.Add(channel, internalEventCollection = new InternalEventCollection());

                internalEventCollection.AddListener(eventType, listener);
            }

            public void RemoveListener(int channel, Type eventType, Delegate listener)
            {
                if (!_value.TryGetValue(channel, out var collection))
                    return;

                collection.RemoveListener(eventType, listener);
                if (collection.Count == 0)
                    _value.Remove(channel);
            }

            public bool Invoke<T>(int channel, T eventData, bool requireReceiver)
            {
                if (_value.TryGetValue(channel, out var collection))
                {
                    var invoked = collection.Invoke(eventData);
                    if (!invoked && requireReceiver)
                        Logger.LogError($"Unable to find any listener for event '{typeof(T).FullName}' that is registered for channel '{channel}'.");

                    return invoked;
                }

                if (requireReceiver)
                    Logger.LogError($"Unable to invoke event '{typeof(T).FullName}'. Make sure receiver (handler) is registered for channel '{channel}', or set '{nameof(requireReceiver)}' to 'false'.");

                return false;
            }

            public void Clear()
            {
                foreach (var internalEventCollection in _value.Values)
                    internalEventCollection.Clear();

                _value.Clear();
            }

            public IEnumerator<KeyValuePair<int, IReadOnlyEventCollection>> GetEnumerator()
            {
                foreach (var kvp in _value)
                    yield return new KeyValuePair<int, IReadOnlyEventCollection>(kvp.Key, kvp.Value);
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }

        private sealed class InternalEventCollection : IReadOnlyEventCollection
        {
            public int Count => _value.Count;
            private readonly Dictionary<Type, InternalEvent> _value = new();

            public void AddListener(Type eventType, Delegate listener)
            {
                if (!_value.TryGetValue(eventType, out var targetEvent))
                    _value.Add(eventType, targetEvent = new InternalEvent());

                targetEvent.AddListener(listener);
            }

            public bool Invoke<T>(T eventData)
            {
                if (!_value.TryGetValue(typeof(T), out var targetEvent))
                    return false;

                targetEvent.Invoke(eventData);
                return true;
            }

            public void RemoveListener(Type eventType, Delegate listener)
            {
                if (!_value.TryGetValue(eventType, out var targetEvent))
                    return;

                targetEvent.RemoveListener(listener);
                if (targetEvent.IsEmpty)
                    _value.Remove(eventType);
            }

            public void Clear()
            {
                foreach (var internalEvent in _value.Values)
                    internalEvent.RemoveAllListeners();

                _value.Clear();
            }

            public IEnumerator<KeyValuePair<Type, IReadOnlyEvent>> GetEnumerator()
            {
                foreach (var kvp in _value)
                    yield return new KeyValuePair<Type, IReadOnlyEvent>(kvp.Key, kvp.Value);
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }

        private sealed class InternalEvent : IReadOnlyEvent
        {
            // Always an Action<T> for the T this event is keyed by, or null.
            private Delegate _value;

            public bool IsEmpty => _value == null;

            // Debug view only; allocates. Do not call it on hot paths.
            public int ListenerCount =>
                _value == null ? 0 : _value.GetInvocationList().Length;

            public void AddListener(Delegate callback) =>
                _value = Delegate.Combine(_value, callback);

            public void RemoveListener(Delegate callback) =>
                _value = Delegate.Remove(_value, callback);

            public void RemoveAllListeners() =>
                _value = null;

            public void Invoke<T>(T eventData) =>
                ((Action<T>)_value)?.Invoke(eventData);

            IEnumerable<string> IReadOnlyEvent.GetListeners()
            {
                if (_value == null)
                    yield break;

                foreach (var handler in _value.GetInvocationList())
                    yield return $"{handler.Method.DeclaringType!.FullName}.{handler.Method.Name}";
            }
        }

        public IReadOnlyChannelEventCollection ChannelEventCollection => _eventCollectionPerChannel;
        private readonly InternalChannelEventCollection _eventCollectionPerChannel = new();

        public void Clear() =>
            _eventCollectionPerChannel.Clear();

        #region AUTO_REGISTRATION
        public void RegisterObject(object targetObject)
        {
            foreach (var listener in EventListenerMethodCache.GetMethods(targetObject.GetType()))
                _eventCollectionPerChannel.AddListener(listener.Channel, listener.EventType, listener.CreateDelegate(targetObject));
        }

        public void UnregisterObject(object targetObject)
        {
            foreach (var listener in EventListenerMethodCache.GetMethods(targetObject.GetType()))
                _eventCollectionPerChannel.RemoveListener(listener.Channel, listener.EventType, listener.CreateDelegate(targetObject));
        }
        #endregion

        #region ADD
        public void AddListener<T>(Action<T> listener) where T : struct =>
            AddListener(listener, channel: 0);

        public void AddListener<T>(Action<T> listener, int channel) where T : struct =>
            _eventCollectionPerChannel.AddListener(channel, typeof(T), listener ?? throw new ArgumentNullException(nameof(listener)));
        #endregion

        #region REMOVE
        public void RemoveListener<T>(Action<T> listener) where T : struct =>
            RemoveListener(listener, channel: 0);

        public void RemoveListener<T>(Action<T> listener, int channel) where T : struct =>
            _eventCollectionPerChannel.RemoveListener(channel, typeof(T), listener);
        #endregion

        // Keep requireReceiver's default in sync with IBroadcaster: C# takes it from the type the call goes through.
        #region INVOKE
        public bool Invoke<T>(T eventData, bool requireReceiver = false) where T : struct =>
            Invoke(eventData, channel: 0, requireReceiver);

        public bool Invoke<T>(T eventData, int channel, bool requireReceiver = false) where T : struct =>
            _eventCollectionPerChannel.Invoke(channel, eventData, requireReceiver);
        #endregion
    }
}