namespace Core.DependencyInjection.Tests
{
    public class BindToSelfTests
    {
        [Fact]
        public void BindToSelf_Singleton_ReturnsSameInstance()
        {
            var binder = new ScopeBinder();
            binder.BindToSelf<ConcreteService>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var service1 = rootScope.Provide<ConcreteService>();
            var service2 = rootScope.Provide<ConcreteService>();

            Assert.NotNull(service1);
            Assert.NotNull(service2);
            Assert.Equal(service1, service2);
        }

        [Fact]
        public void BindToSelf_Transient_ReturnsNewInstances()
        {
            var binder = new ScopeBinder();
            binder.BindToSelf<ConcreteService>(ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());
            var service1 = rootScope.Provide<ConcreteService>();
            var service2 = rootScope.Provide<ConcreteService>();

            Assert.NotNull(service1);
            Assert.NotNull(service2);
            Assert.NotEqual(service1, service2);
        }

        [Fact]
        public void BindToSelf_WithDependencies_ResolvesCorrectly()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            binder.BindToSelf<ServiceWithDependencies>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var service = rootScope.Provide<ServiceWithDependencies>();

            Assert.NotNull(service);
            Assert.NotNull(service.FirstServiceProperty);
            Assert.IsAssignableFrom<FirstService>(service.FirstServiceProperty);
        }

        [Fact]
        public void BindToSelf_InChildScope_IsolatesInstance()
        {
            var rootBinder = new ScopeBinder();
            rootBinder.BindToSelf<ConcreteService>();
            var rootScope = new RootScope(rootBinder.ToDependencyMap());

            var childBinder = new ScopeBinder();
            childBinder.BindToSelf<ConcreteService>();
            var childScope = rootScope.CreateChildScope(childBinder.ToDependencyMap());

            var rootService = rootScope.Provide<ConcreteService>();
            var childService = childScope.Provide<ConcreteService>();

            Assert.NotNull(rootService);
            Assert.NotNull(childService);
            Assert.NotEqual(rootService, childService);
        }

        [Fact]
        public void BindToSelf_MultipleServices_ResolvesIndependently()
        {
            var binder = new ScopeBinder();
            binder.BindToSelf<FirstConcreteService>();
            binder.BindToSelf<SecondConcreteService>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var service1 = rootScope.Provide<FirstConcreteService>();
            var service2 = rootScope.Provide<SecondConcreteService>();

            Assert.NotNull(service1);
            Assert.NotNull(service2);
            Assert.IsType<FirstConcreteService>(service1);
            Assert.IsType<SecondConcreteService>(service2);
        }

        private sealed class ConcreteService;

        private sealed class FirstConcreteService;

        private sealed class SecondConcreteService;

        private sealed class ServiceWithDependencies
        {
            [Inject]
            public IFirstService FirstServiceProperty { get; } = null;
        }

        private sealed class FirstService : IFirstService;

        [DefaultImplementation(typeof(FirstService))]
        private interface IFirstService;
    }
}