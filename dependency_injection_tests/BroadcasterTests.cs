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

            broadcaster.Invoke(channel: 1, new TextEvent("Channel 1 Data"));
            broadcaster.Invoke(channel: 2, new TextEvent("Channel 2 Data"));

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

            broadcaster.Invoke(channel: 1, new TextEvent("Text Event"));
            broadcaster.Invoke(channel: 1, new OtherTextEvent("Other Text Event"));

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
            broadcaster.Invoke(channel: 1, new TextEvent("Test Data"), requireReceiver: false);
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

            broadcaster.Invoke(channel: 1, new MultiFieldEvent("StringValue", 42, true));

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
            broadcaster.Invoke(channel: 1, new TextEvent("First Message"));
            Assert.Equal("First Message", listener.EventData);

            // Unregister
            listener.Unregister();

            // Send event after unregistration
            broadcaster.Invoke(channel: 1, new TextEvent("Second Message"));
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

            broadcaster.Invoke(channel: 1, new EmptyEvent());

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

            broadcaster.Invoke(channel: 1, new NumberEvent(10));
            broadcaster.Invoke(channel: 1, new NumberEvent(20));
            broadcaster.Invoke(channel: 1, new NumberEvent(30));

            Assert.Equal(3, listener.InvocationCount);
            Assert.Equal(60, listener.Sum);
        }

        private sealed class FirstService
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;

            public void SendEvent(string eventData) =>
                _broadcaster.Invoke(channel: 1, new TextEvent(eventData));
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
            broadcaster.Invoke(channel: 1, new TextEvent(testData));

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
            broadcaster.Invoke(channel: 1, new TextEvent("First"));
            Assert.Equal("First", listener.Data);
            Assert.Equal(1, listener.CallCount);

            // Unregister
            listener.Unregister();

            // Event while unregistered (should not be received)
            broadcaster.Invoke(channel: 1, new TextEvent("Second"));
            Assert.Equal("First", listener.Data); // Still first
            Assert.Equal(1, listener.CallCount); // Count unchanged

            // Reregister
            listener.Register();

            // Event after reregistration
            broadcaster.Invoke(channel: 1, new TextEvent("Third"));
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

            broadcaster.AddListener<TextEvent>(channel: 1, e => received.Add(e.Text));

            Assert.False(broadcaster.Invoke(new TextEvent("Channel 0")));
            Assert.True(broadcaster.Invoke(channel: 1, new TextEvent("Channel 1")));

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
        public void Test_InvalidListenerSignature_IsSkipped_AndLogged()
        {
            var errors = new ConcurrentQueue<string>();
            var invalidListenerName = $"{nameof(InvalidSignatureListener)}.{InvalidSignatureListener.InvalidMethodName}";

            void OnLogReceived(LogLevel level, string message)
            {
                if (level == LogLevel.Error && message.Contains(invalidListenerName))
                    errors.Enqueue(message);
            }

            Logger.LogReceivedThreaded += OnLogReceived;
            try
            {
                var broadcaster = new Broadcaster();
                var listener = new InvalidSignatureListener();

                broadcaster.RegisterObject(listener);
                Assert.True(broadcaster.Invoke(new TextEvent("Valid")));
                Assert.Equal("Valid", listener.ValidData);

                // The method cache is per type, so a second registration must not log again.
                broadcaster.RegisterObject(new InvalidSignatureListener());
            }
            finally
            {
                Logger.LogReceivedThreaded -= OnLogReceived;
            }

            Assert.Single(errors);
        }

        [Fact]
        public void Test_AddListener_Null_Throws()
        {
            var broadcaster = new Broadcaster();

            Assert.Throws<ArgumentNullException>(() => broadcaster.AddListener<TextEvent>(null));
            Assert.Throws<ArgumentNullException>(() => broadcaster.AddListener<TextEvent>(channel: 1, null));

            Assert.False(broadcaster.Invoke(new TextEvent("Nobody")));
            Assert.False(broadcaster.Invoke(channel: 1, new TextEvent("Nobody")));
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
    }
}