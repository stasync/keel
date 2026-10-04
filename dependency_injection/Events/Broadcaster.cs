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

            public void AddListener(int channel, int eventCode, Action<object[]> listener)
            {
                if (!_value.TryGetValue(channel, out var internalEventCollection))
                    _value.Add(channel, internalEventCollection = new InternalEventCollection());

                internalEventCollection.AddListener(eventCode, listener);
            }

            public void RemoveListener(int channel, int eventCode, Action<object[]> listener)
            {
                if (!_value.TryGetValue(channel, out var collection))
                    return;

                collection.RemoveListener(eventCode, listener);
                if (collection.Count == 0)
                    _value.Remove(channel);
            }

            public bool Invoke(int channel, int eventCode, bool requireReceiver, params object[] args)
            {
                if (_value.TryGetValue(channel, out var collection))
                {
                    var invoked = collection.Invoke(eventCode, args);
                    if (!invoked && requireReceiver)
                        Logger.LogError($"Unable to find any event with code '{eventCode}' that is registered for channel '{channel}'.");

                    return invoked;
                }

                if (requireReceiver)
                    Logger.LogError($"Unable to invoke event with code '{eventCode}'. Make sure receiver (handler) is registered for channel '{channel}', or set '{nameof(requireReceiver)}' to 'false'.");

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
            private readonly Dictionary<int, InternalEvent> _value = new();

            public void AddListener(int eventCode, Action<object[]> listener)
            {
                if (!_value.TryGetValue(eventCode, out var targetEvent))
                    _value.Add(eventCode, targetEvent = new InternalEvent());

                targetEvent.AddListener(listener);
            }

            public bool Invoke(int eventCode, params object[] args)
            {
                if (_value.TryGetValue(eventCode, out var targetEvent))
                    targetEvent.Invoke(args);

                return targetEvent != null;
            }

            public void RemoveListener(int eventCode, Action<object[]> listener)
            {
                if (!_value.TryGetValue(eventCode, out var targetEvent))
                    return;

                targetEvent.RemoveListener(listener);
                if (targetEvent.ListenerCount == 0)
                    _value.Remove(eventCode);
            }

            public void Clear()
            {
                foreach (var internalEvent in _value.Values)
                    internalEvent.RemoveAllListeners();

                _value.Clear();
            }

            public IEnumerator<KeyValuePair<int, IReadOnlyEvent>> GetEnumerator()
            {
                foreach (var kvp in _value)
                    yield return new KeyValuePair<int, IReadOnlyEvent>(kvp.Key, kvp.Value);
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }

        private sealed class InternalEvent : IReadOnlyEvent
        {
            private event Action<object[]> Value;

            public int ListenerCount =>
                Value == null ? 0 : Value.GetInvocationList().Length;

            public void AddListener(Action<object[]> callback) =>
                Value += callback;

            public void RemoveListener(Action<object[]> callback) =>
                Value -= callback;

            public void RemoveAllListeners() =>
                Value = null;

            public void Invoke(params object[] args) =>
                Value?.Invoke(args);

            IEnumerable<string> IReadOnlyEvent.GetListeners()
            {
                if (Value == null)
                    yield break;

                foreach (var handler in Value.GetInvocationList())
                    yield return $"{handler.Method.DeclaringType!.FullName}.{handler.Method.Name}";
            }
        }

        public IReadOnlyChannelEventCollection ChannelEventCollection => _eventCollectionPerChannel;
        private readonly InternalChannelEventCollection _eventCollectionPerChannel = new();

        public void Clear() =>
            _eventCollectionPerChannel.Clear();

        #region AUTO_REGISTRATION
        public void RegisterObject(object targetObject) =>
            ProcessObject(targetObject, AddListener);

        public void UnregisterObject(object targetObject) =>
            ProcessObject(targetObject, RemoveListener);

        private static void ProcessObject(object targetObject, Action<int, ushort, Action<object[]>> processor)
        {
            foreach (var (methodInfo, attribute) in EventListenerMethodCache.GetMethods(targetObject.GetType()))
            {
                if (methodInfo.CreateDelegate(typeof(Action<object[]>), targetObject) is Action<object[]> action)
                    processor(attribute.Channel, attribute.EventCode, action);
            }
        }
        #endregion

        #region ADD
        public void AddListener(ushort eventCode, Action<object[]> listener) =>
            AddListener(channel: 0, eventCode, listener);

        public void AddListener(int channel, ushort eventCode, Action<object[]> listener) =>
            _eventCollectionPerChannel.AddListener(channel, eventCode, listener);
        #endregion

        #region REMOVE
        public void RemoveListener(ushort eventCode, Action<object[]> listener) =>
            RemoveListener(channel: 0, eventCode, listener);

        public void RemoveListener(int channel, ushort eventCode, Action<object[]> listener) =>
            _eventCollectionPerChannel.RemoveListener(channel, eventCode, listener);
        #endregion

        #region INVOKE
        public bool Invoke(ushort eventCode, bool requireReceiver, params object[] args) =>
            Invoke(channel: 0, eventCode, requireReceiver, args);

        public bool Invoke(int channel, ushort eventCode, bool requireReceiver, params object[] args) =>
            _eventCollectionPerChannel.Invoke(channel, eventCode, requireReceiver, args);
        #endregion
    }
}