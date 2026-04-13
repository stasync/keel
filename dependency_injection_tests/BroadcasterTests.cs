using Core.DependencyInjection.Events;
using Core.DependencyInjection.Interface;

namespace Core.DependencyInjection.Tests
{
    public class BroadcasterTests
    {
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

            broadcaster.Invoke(channel: 1, eventCode: 200, requireReceiver: false, "Channel 1 Data");
            broadcaster.Invoke(channel: 2, eventCode: 200, requireReceiver: false, "Channel 2 Data");

            Assert.Equal("Channel 1 Data", listener.Channel1Data);
            Assert.Equal("Channel 2 Data", listener.Channel2Data);
        }

        [Fact]
        public void Test_DifferentEventCodes_RouteCorrectly()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();
            binder.BindToSelf<MultiEventListener>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var listener = rootScope.Provide<MultiEventListener>();
            var broadcaster = rootScope.Provide<IBroadcaster>();

            broadcaster.Invoke(channel: 1, eventCode: 100, requireReceiver: false, "Event 100");
            broadcaster.Invoke(channel: 1, eventCode: 200, requireReceiver: false, "Event 200");

            Assert.Equal("Event 100", listener.Event100Data);
            Assert.Equal("Event 200", listener.Event200Data);
        }

        [Fact]
        public void Test_RequireReceiver_False_DoesNotThrow_WhenNoListeners()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IBroadcaster>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var broadcaster = rootScope.Provide<IBroadcaster>();

            // Should not throw even with no listeners registered
            broadcaster.Invoke(channel: 1, eventCode: 999, requireReceiver: false, "Test Data");
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

            broadcaster.Invoke(channel: 1, eventCode: 300, requireReceiver: false, "StringValue", 42, true);

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
            broadcaster.Invoke(channel: 1, eventCode: 400, requireReceiver: false, "First Message");
            Assert.Equal("First Message", listener.EventData);

            // Unregister
            listener.Unregister();

            // Send event after unregistration
            broadcaster.Invoke(channel: 1, eventCode: 400, requireReceiver: false, "Second Message");
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

            broadcaster.Invoke(channel: 1, eventCode: 500, requireReceiver: false);

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

            broadcaster.Invoke(channel: 1, eventCode: 600, requireReceiver: false, 10);
            broadcaster.Invoke(channel: 1, eventCode: 600, requireReceiver: false, 20);
            broadcaster.Invoke(channel: 1, eventCode: 600, requireReceiver: false, 30);

            Assert.Equal(3, listener.InvocationCount);
            Assert.Equal(60, listener.Sum);
        }

        private sealed class FirstService
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;

            public void SendEvent(string eventData) =>
                _broadcaster.Invoke(channel: 1, eventCode: 100, requireReceiver: false, eventData);
        }

        private sealed class SecondScopeListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string EventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 100, channel: 1)]
            private void OnEventReceive(object[] parameters)
            {
                var p = new EventParameters(parameters);
                EventData = p.Next<string>();
            }
        }

        private sealed class ThirdScopeListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string EventData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 100, channel: 1)]
            private void OnEventReceive(object[] parameters)
            {
                var p = new EventParameters(parameters);
                EventData = p.Next<string>();
            }
        }

        private sealed class MultiChannelListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string Channel1Data;
            public string Channel2Data;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 200, channel: 1)]
            private void OnChannel1Event(object[] parameters)
            {
                var p = new EventParameters(parameters);
                Channel1Data = p.Next<string>();
            }

            [EventListener(eventCode: 200, channel: 2)]
            private void OnChannel2Event(object[] parameters)
            {
                var p = new EventParameters(parameters);
                Channel2Data = p.Next<string>();
            }
        }

        private sealed class MultiEventListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string Event100Data;
            public string Event200Data;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 100, channel: 1)]
            private void OnEvent100(object[] parameters)
            {
                var p = new EventParameters(parameters);
                Event100Data = p.Next<string>();
            }

            [EventListener(eventCode: 200, channel: 1)]
            private void OnEvent200(object[] parameters)
            {
                var p = new EventParameters(parameters);
                Event200Data = p.Next<string>();
            }
        }

        private sealed class MultiParameterListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string StringParam;
            public int IntParam;
            public bool BoolParam;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 300, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                var p = new EventParameters(parameters);
                StringParam = p.Next<string>();
                IntParam = p.Next<int>();
                BoolParam = p.Next<bool>();
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

            [EventListener(eventCode: 400, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                var p = new EventParameters(parameters);
                EventData = p.Next<string>();
            }
        }

        private sealed class NoParameterListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public bool EventReceived;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 500, channel: 1)]
            private void OnEvent(object[] parameters) =>
                EventReceived = true;
        }

        private sealed class CountingListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public int InvocationCount;
            public int Sum;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 600, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                InvocationCount++;
                var p = new EventParameters(parameters);
                Sum += p.Next<int>();
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
            broadcaster.Invoke(channel: 1, eventCode: 700, requireReceiver: false, testData);

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
            broadcaster.Invoke(channel: 1, eventCode: 900, requireReceiver: false, "First");
            Assert.Equal("First", listener.Data);
            Assert.Equal(1, listener.CallCount);

            // Unregister
            listener.Unregister();

            // Event while unregistered (should not be received)
            broadcaster.Invoke(channel: 1, eventCode: 900, requireReceiver: false, "Second");
            Assert.Equal("First", listener.Data); // Still first
            Assert.Equal(1, listener.CallCount); // Count unchanged

            // Reregister
            listener.Register();

            // Event after reregistration
            broadcaster.Invoke(channel: 1, eventCode: 900, requireReceiver: false, "Third");
            Assert.Equal("Third", listener.Data);
            Assert.Equal(2, listener.CallCount);
        }

        private sealed class FirstDuplicateListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string ReceivedData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 700, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                var p = new EventParameters(parameters);
                ReceivedData = p.Next<string>();
            }
        }

        private sealed class SecondDuplicateListener : IScopeListener
        {
            [Inject] private readonly IBroadcaster _broadcaster = null;
            public string ReceivedData;

            public void OnResolved() =>
                _broadcaster.RegisterObject(this);

            [EventListener(eventCode: 700, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                var p = new EventParameters(parameters);
                ReceivedData = p.Next<string>();
            }
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

            [EventListener(eventCode: 900, channel: 1)]
            private void OnEvent(object[] parameters)
            {
                CallCount++;
                var p = new EventParameters(parameters);
                Data = p.Next<string>();
            }
        }
    }
}