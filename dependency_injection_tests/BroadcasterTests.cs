using System.Collections.Concurrent;
using Keel.DependencyInjection.Events;
using Keel.DependencyInjection.Interface;
using Keel.Utils.Debug;

namespace Keel.DependencyInjection.Tests
{
    public class BroadcasterTests
    {
        private readonly record struct TextEvent(string Text);
        private readonly record struct OtherTextEvent(string Text);
        private readonly record struct MultiFieldEvent(string Text, int Number, bool Flag);
        private readonly record struct NumberEvent(int Value);
        private readonly struct EmptyEvent { }

        [Fact]
        public void Test_BasicBroadcast_Delivers_ToMultipleListeners()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<FirstService>();
            binder.BindToSelf<SecondScopeListener>();
            binder.BindToSelf<ThirdScopeListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var fistService = rootScope.Provide<FirstService>();
            var secondService = rootScope.Provide<SecondScopeListener>();
            var thirdService = rootScope.Provide<ThirdScopeListener>();

            const string eventData = "HELLO WORLD";
            fistService.SendEvent(eventData);

            Assert.Equal(eventData, secondService.EventData);
            Assert.Equal(eventData, thirdService.EventData);
        }

        [Fact]
        public void Test_DifferentChannels_DoNotInterfere()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<MultiChannelListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<MultiChannelListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(new TextEvent("Channel 1 Data"), channel: 1);
            broadcaster.Invoke(new TextEvent("Channel 2 Data"), channel: 2);

            Assert.Equal("Channel 1 Data", listener.Channel1Data);
            Assert.Equal("Channel 2 Data", listener.Channel2Data);
        }

        [Fact]
        public void Test_DifferentEventTypes_RouteCorrectly()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<MultiEventListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<MultiEventListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(new TextEvent("Text Event"), channel: 1);
            broadcaster.Invoke(new OtherTextEvent("Other Text Event"), channel: 1);

            Assert.Equal("Text Event", listener.TextEventData);
            Assert.Equal("Other Text Event", listener.OtherTextEventData);
        }

        [Fact]
        public void Test_RequireReceiver_False_DoesNotThrow_WhenNoListeners()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var broadcaster = rootScope.Provide<IBroadcaster>();

            // Should not throw even with no listeners registered
            broadcaster.Invoke(new TextEvent("Test Data"), channel: 1, requireReceiver: false);
        }

        [Fact]
        public void Test_MultipleParameters_AreReceivedCorrectly()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<MultiParameterListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<MultiParameterListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(new MultiFieldEvent("StringValue", 42, true), channel: 1);

            Assert.Equal("StringValue", listener.StringParam);
            Assert.Equal(42, listener.IntParam);
            Assert.True(listener.BoolParam);
        }

        [Fact]
        public void Test_UnregisteredListener_DoesNotReceiveEvents()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<UnregisterableListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<UnregisterableListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            // Send event while registered
            broadcaster.Invoke(new TextEvent("First Message"), channel: 1);
            Assert.Equal("First Message", listener.EventData);

            // Unregister
            listener.Unregister();

            // Send event after unregistration
            broadcaster.Invoke(new TextEvent("Second Message"), channel: 1);
            Assert.Equal("First Message", listener.EventData); // Should still be first message
        }

        [Fact]
        public void Test_EventInvocation_WithNoParameters()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<NoParameterListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<NoParameterListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(new EmptyEvent(), channel: 1);

            Assert.True(listener.EventReceived);
        }

        [Fact]
        public void Test_MultipleInvocations_UpdatesListener()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<CountingListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<CountingListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(new NumberEvent(10), channel: 1);
            broadcaster.Invoke(new NumberEvent(20), channel: 1);
            broadcaster.Invoke(new NumberEvent(30), channel: 1);

            Assert.Equal(3, listener.InvocationCount);
            Assert.Equal(60, listener.Sum);
        }

        private sealed class FirstService
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;

            public void SendEvent(string eventData) =>
                _broadcaster.Invoke(new TextEvent(eventData), channel: 1);
        }

        private sealed class SecondScopeListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string EventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEventReceive(TextEvent e) =>
                EventData = e.Text;
        }

        private sealed class ThirdScopeListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string EventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEventReceive(TextEvent e) =>
                EventData = e.Text;
        }

        private sealed class MultiChannelListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string Channel1Data;
            public string Channel2Data;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnChannel1Event(TextEvent e) =>
                Channel1Data = e.Text;

            [EventListener(channel: 2)]
            private void OnChannel2Event(TextEvent e) =>
                Channel2Data = e.Text;
        }

        private sealed class MultiEventListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string TextEventData;
            public string OtherTextEventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnTextEvent(TextEvent e) =>
                TextEventData = e.Text;

            [EventListener(channel: 1)]
            private void OnOtherTextEvent(OtherTextEvent e) =>
                OtherTextEventData = e.Text;
        }

        private sealed class MultiParameterListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string StringParam;
            public int IntParam;
            public bool BoolParam;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(MultiFieldEvent e)
            {
                StringParam = e.Text;
                IntParam = e.Number;
                BoolParam = e.Flag;
            }
        }

        private sealed class UnregisterableListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string EventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            public void Unregister() =>
                _broadcaster.UnregisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(TextEvent e) =>
                EventData = e.Text;
        }

        private sealed class NoParameterListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public bool EventReceived;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(EmptyEvent _) =>
                EventReceived = true;
        }

        private sealed class CountingListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public int InvocationCount;
            public int Sum;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(NumberEvent e)
            {
                InvocationCount++;
                Sum += e.Value;
            }
        }

        [Fact]
        public void Test_MultipleListeners_SameEventChannel_AllReceive()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<FirstDuplicateListener>();
            binder.BindToSelf<SecondDuplicateListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener1 = rootScope.Provide<FirstDuplicateListener>();
            var listener2 = rootScope.Provide<SecondDuplicateListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            const string testData = "Broadcast to all";
            broadcaster.Invoke(new TextEvent(testData), channel: 1);

            // Both listeners should receive the same event
            Assert.Equal(testData, listener1.ReceivedData);
            Assert.Equal(testData, listener2.ReceivedData);
        }

        [Fact]
        public void Test_Reregistration_AfterUnregister_Works()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<ReregisterableListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<ReregisterableListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            // First event
            broadcaster.Invoke(new TextEvent("First"), channel: 1);
            Assert.Equal("First", listener.Data);
            Assert.Equal(1, listener.CallCount);

            // Unregister
            listener.Unregister();

            // Event while unregistered (should not be received)
            broadcaster.Invoke(new TextEvent("Second"), channel: 1);
            Assert.Equal("First", listener.Data); // Still first
            Assert.Equal(1, listener.CallCount); // Count unchanged

            // Reregister
            listener.Register();

            // Event after reregistration
            broadcaster.Invoke(new TextEvent("Third"), channel: 1);
            Assert.Equal("Third", listener.Data);
            Assert.Equal(2, listener.CallCount);
        }

        private sealed class FirstDuplicateListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string ReceivedData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(TextEvent e) =>
                ReceivedData = e.Text;
        }

        private sealed class SecondDuplicateListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string ReceivedData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(TextEvent e) =>
                ReceivedData = e.Text;
        }

        private sealed class ReregisterableListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string Data;
            public int CallCount;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            public void Register() =>
                _broadcaster.RegisterObject(this);

            public void Unregister() =>
                _broadcaster.UnregisterObject(this);

            [EventListener(channel: 1)]
            private void OnEvent(TextEvent e)
            {
                CallCount++;
                Data = e.Text;
            }
        }

        [Fact]
        public void Test_Invoke_ReturnsWhetherAnyoneListened()
        {
            var broadcaster = new Broadcaster();

            Assert.False(broadcaster.Invoke(new TextEvent("Nobody")));

            broadcaster.AddListener<TextEvent>(_ => { });

            Assert.True(broadcaster.Invoke(new TextEvent("Somebody")));
        }

        [Fact]
        public void Test_ManualListener_ReceivesEvents_UntilRemoved()
        {
            var broadcaster = new Broadcaster();
            var received = new List<string>();
            Action<TextEvent> listener = e => received.Add(e.Text);

            broadcaster.AddListener(listener);
            Assert.True(broadcaster.Invoke(new TextEvent("First")));

            broadcaster.RemoveListener(listener);
            Assert.False(broadcaster.Invoke(new TextEvent("Second")));

            Assert.Equal(["First"], received);
        }

        [Fact]
        public void Test_ManualListener_OnOtherChannel_DoesNotReceive()
        {
            var broadcaster = new Broadcaster();
            var received = new List<string>();

            // Channel passed positionally on purpose: it must bind to the channel overloads.
            broadcaster.AddListener<TextEvent>(e => received.Add(e.Text), 1);

            Assert.False(broadcaster.Invoke(new TextEvent("Channel 0")));
            Assert.True(broadcaster.Invoke(new TextEvent("Channel 1"), 1));

            Assert.Equal(["Channel 1"], received);
        }

        [Fact]
        public void Test_PrivateBaseClassListener_IsRegistered()
        {
            var broadcaster = new Broadcaster();
            var listener = new DerivedFromPrivateListenerBase();

            broadcaster.RegisterObject(listener);
            Assert.True(broadcaster.Invoke(new TextEvent("From base")));
            Assert.Equal("From base", listener.BaseData);

            broadcaster.UnregisterObject(listener);
            Assert.False(broadcaster.Invoke(new TextEvent("After unregister")));
            Assert.Equal("From base", listener.BaseData);
        }

        [Fact]
        public void Test_OverriddenVirtualListener_IsCalledOnce_AndRunsOverride()
        {
            var broadcaster = new Broadcaster();
            var listener = new OverridingListener();

            broadcaster.RegisterObject(listener);
            broadcaster.Invoke(new TextEvent("First"));
            broadcaster.Invoke(new TextEvent("Second"));

            Assert.Equal(["Derived: First", "Derived: Second"], listener.Calls);

            broadcaster.UnregisterObject(listener);
            Assert.False(broadcaster.Invoke(new TextEvent("After unregister")));
        }

        [Fact]
        public void Test_VirtualListener_AttributeOnBaseOnly_RunsOverride()
        {
            var broadcaster = new Broadcaster();
            var listener = new OverridingWithoutAttributeListener();

            broadcaster.RegisterObject(listener);
            broadcaster.Invoke(new TextEvent("First"));

            Assert.Equal(["Derived: First"], listener.Calls);

            broadcaster.UnregisterObject(listener);
            Assert.False(broadcaster.Invoke(new TextEvent("After unregister")));
        }

        [Fact]
        public void Test_VirtualListener_DifferentChannelsOnBaseAndOverride_SubscribesBoth()
        {
            var broadcaster = new Broadcaster();
            var listener = new OverridingOnOtherChannelListener();

            broadcaster.RegisterObject(listener);
            Assert.True(broadcaster.Invoke(new TextEvent("One"), channel: 1));
            Assert.True(broadcaster.Invoke(new TextEvent("Two"), channel: 2));

            Assert.Equal(["Derived: One", "Derived: Two"], listener.Calls);

            broadcaster.UnregisterObject(listener);
            Assert.False(broadcaster.Invoke(new TextEvent("After unregister"), channel: 1));
            Assert.False(broadcaster.Invoke(new TextEvent("After unregister"), channel: 2));
        }

        [Fact]
        public void Test_InvalidListenerSignature_IsSkipped_AndLogged()
        {
            var broadcaster = new Broadcaster();
            var listener = new InvalidSignatureListener();

            var errors = CaptureLogs(LogLevel.Error, $"{nameof(InvalidSignatureListener)}.{InvalidSignatureListener.InvalidMethodName}", () =>
            {
                broadcaster.RegisterObject(listener);

                // The method cache is per type, so a second registration must not log again.
                broadcaster.RegisterObject(new InvalidSignatureListener());
            });

            Assert.Single(errors);
            Assert.True(broadcaster.Invoke(new TextEvent("Valid")));
            Assert.Equal("Valid", listener.ValidData);
        }

        [Fact]
        public void Test_RefStructListener_IsSkipped_AndLogged()
        {
            var broadcaster = new Broadcaster();

            var errors = CaptureLogs(LogLevel.Error, $"{nameof(RefStructListener)}.{RefStructListener.MethodName}", () =>
                broadcaster.RegisterObject(new RefStructListener()));

            Assert.Single(errors);
            Assert.Empty(broadcaster.ChannelEventCollection);
        }

        [Fact]
        public void Test_AddListener_Null_Throws()
        {
            var broadcaster = new Broadcaster();

            Assert.Throws<ArgumentNullException>(() => broadcaster.AddListener<TextEvent>(null));
            Assert.Throws<ArgumentNullException>(() => broadcaster.AddListener<TextEvent>(null, channel: 1));

            Assert.False(broadcaster.Invoke(new TextEvent("Nobody")));
            Assert.False(broadcaster.Invoke(new TextEvent("Nobody"), channel: 1));
        }

        [Fact]
        public void Test_ThrowingListener_IsLogged_AndDoesNotStopOthers()
        {
            const string failure = "Listener failure from " + nameof(Test_ThrowingListener_IsLogged_AndDoesNotStopOthers);
            var broadcaster = new Broadcaster();
            var calls = new List<string>();

            broadcaster.AddListener<TextEvent>(_ => calls.Add("First"));
            broadcaster.AddListener<TextEvent>(_ => throw new InvalidOperationException(failure));
            broadcaster.AddListener<TextEvent>(_ => calls.Add("Third"));

            var exceptions = CaptureLogs(LogLevel.Exception, failure, () =>
                Assert.True(broadcaster.Invoke(new TextEvent("Dispatch"))));

            Assert.Equal(["First", "Third"], calls);
            Assert.Single(exceptions);
        }

        [Fact]
        public void Test_ListenerRemovingItself_DuringDispatch_DoesNotStopOthers()
        {
            var broadcaster = new Broadcaster();
            var calls = new List<string>();

            Action<TextEvent> selfRemoving = null;
            selfRemoving = _ =>
            {
                calls.Add("SelfRemoving");
                broadcaster.RemoveListener(selfRemoving);
            };

            broadcaster.AddListener(selfRemoving);
            broadcaster.AddListener<TextEvent>(_ => calls.Add("Second"));
            broadcaster.AddListener<TextEvent>(_ => calls.Add("Third"));

            broadcaster.Invoke(new TextEvent("First dispatch"));
            Assert.Equal(["SelfRemoving", "Second", "Third"], calls);

            calls.Clear();
            broadcaster.Invoke(new TextEvent("Second dispatch"));
            Assert.Equal(["Second", "Third"], calls);
        }

        [Fact]
        public void Test_ListenersChanged_DuringDispatch_TakeEffectFromNextInvoke()
        {
            var broadcaster = new Broadcaster();
            var calls = new List<string>();
            var changed = false;

            Action<TextEvent> removed = _ => calls.Add("Removed");
            Action<TextEvent> added = _ => calls.Add("Added");

            broadcaster.AddListener<TextEvent>(_ =>
            {
                calls.Add("Changer");
                if (changed)
                    return;

                changed = true;
                broadcaster.RemoveListener(removed);
                broadcaster.AddListener(added);
            });
            broadcaster.AddListener(removed);

            broadcaster.Invoke(new TextEvent("First dispatch"));
            Assert.Equal(["Changer", "Removed"], calls);

            calls.Clear();
            broadcaster.Invoke(new TextEvent("Second dispatch"));
            Assert.Equal(["Changer", "Added"], calls);
        }

        [Fact]
        public void Test_DebugView_TracksListeners_AndDropsEmptyEntries()
        {
            var broadcaster = new Broadcaster();
            Action<TextEvent> first = _ => { };
            Action<TextEvent> second = _ => { };

            broadcaster.AddListener(first, channel: 1);
            broadcaster.AddListener(second, channel: 1);

            var (channel, events) = Assert.Single(broadcaster.ChannelEventCollection);
            Assert.Equal(1, channel);
            var (eventType, targetEvent) = Assert.Single(events);
            Assert.Equal(typeof(TextEvent), eventType);
            Assert.Equal(2, targetEvent.ListenerCount);
            Assert.Equal(2, targetEvent.GetListeners().Count());

            broadcaster.RemoveListener(first, channel: 1);
            Assert.Equal(1, targetEvent.ListenerCount);

            broadcaster.RemoveListener(second, channel: 1);
            Assert.Empty(broadcaster.ChannelEventCollection);
        }

        private static List<string> CaptureLogs(LogLevel level, string filter, Action action)
        {
            // xUnit runs test classes in parallel, so keep only the messages this test is looking for.
            var messages = new ConcurrentQueue<string>();
            void OnLogReceived(LogLevel receivedLevel, string message)
            {
                if (receivedLevel == level && message.Contains(filter))
                    messages.Enqueue(message);
            }

            Logger.LogReceivedThreaded += OnLogReceived;
            try
            {
                action();
            }
            finally
            {
                Logger.LogReceivedThreaded -= OnLogReceived;
            }

            return messages.ToList();
        }

        private class PrivateListenerBase
        {
            public string BaseData;

            [EventListener]
            private void OnBaseEvent(TextEvent e) =>
                BaseData = e.Text;
        }

        private sealed class DerivedFromPrivateListenerBase : PrivateListenerBase
        {
        }

        private class VirtualListenerBase
        {
            public readonly List<string> Calls = new();

            [EventListener]
            protected virtual void OnEvent(TextEvent e) =>
                Calls.Add($"Base: {e.Text}");
        }

        private sealed class OverridingListener : VirtualListenerBase
        {
            [EventListener]
            protected override void OnEvent(TextEvent e) =>
                Calls.Add($"Derived: {e.Text}");
        }

        private sealed class OverridingWithoutAttributeListener : VirtualListenerBase
        {
            protected override void OnEvent(TextEvent e) =>
                Calls.Add($"Derived: {e.Text}");
        }

        private class ChannelVirtualListenerBase
        {
            public readonly List<string> Calls = new();

            [EventListener(channel: 1)]
            protected virtual void OnEvent(TextEvent e) =>
                Calls.Add($"Base: {e.Text}");
        }

        private sealed class OverridingOnOtherChannelListener : ChannelVirtualListenerBase
        {
            [EventListener(channel: 2)]
            protected override void OnEvent(TextEvent e) =>
                Calls.Add($"Derived: {e.Text}");
        }

        // Only Test_InvalidListenerSignature_IsSkipped_AndLogged may register this type:
        // the method cache is static per type, so the error is logged on the first lookup only.
        private sealed class InvalidSignatureListener
        {
            public const string InvalidMethodName = nameof(OnInvalidSignature);
            public string ValidData;

            [EventListener]
            private void OnValid(TextEvent e) =>
                ValidData = e.Text;

            [EventListener]
            private void OnInvalidSignature(string text) =>
                ValidData = text;
        }

        // Only Test_RefStructListener_IsSkipped_AndLogged may register this type, for the same reason.
        private sealed class RefStructListener
        {
            public const string MethodName = nameof(OnSpan);

            [EventListener]
            private void OnSpan(Span<int> values)
            {
            }
        }
    }
}