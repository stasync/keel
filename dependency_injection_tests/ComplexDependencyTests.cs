namespace Keel.DependencyInjection.Tests
{
    public class ComplexDependencyTests
    {
        [Fact]
        public void DeepDependencyChain_ResolvesCorrectly()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IServiceA>();
            binder.BindToDefaultImplementation<IServiceB>();
            binder.BindToDefaultImplementation<IServiceC>();
            binder.BindToDefaultImplementation<IServiceD>();
            binder.BindToDefaultImplementation<IServiceE>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var serviceA = rootScope.Provide<IServiceA>();

            // Verify the entire chain: A → B → C → D → E
            Assert.NotNull(serviceA);
            Assert.NotNull(serviceA.ServiceBProperty);
            Assert.NotNull(serviceA.ServiceBProperty.ServiceCProperty);
            Assert.NotNull(serviceA.ServiceBProperty.ServiceCProperty.ServiceDProperty);
            Assert.NotNull(serviceA.ServiceBProperty.ServiceCProperty.ServiceDProperty.ServiceEProperty);
        }

        [Fact]
        public void DiamondDependencyPattern_SingletonSharesInstance()
        {
            // Pattern: A → B, A → C, B → D, C → D
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IDiamondA>();
            binder.BindToDefaultImplementation<IDiamondB>();
            binder.BindToDefaultImplementation<IDiamondC>();
            binder.BindToDefaultImplementation<IDiamondD>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var serviceA = rootScope.Provide<IDiamondA>();

            // D should be the same instance when accessed through B or C
            Assert.NotNull(serviceA);
            Assert.NotNull(serviceA.ServiceBProperty);
            Assert.NotNull(serviceA.ServiceCProperty);
            Assert.NotNull(serviceA.ServiceBProperty.ServiceDProperty);
            Assert.NotNull(serviceA.ServiceCProperty.ServiceDProperty);
            Assert.Equal(serviceA.ServiceBProperty.ServiceDProperty, serviceA.ServiceCProperty.ServiceDProperty);
        }

        [Fact]
        public void DiamondDependencyPattern_TransientCreatesMultipleInstances()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IDiamondA>(ImplementationBehaviour.Transient);
            binder.BindToDefaultImplementation<IDiamondB>(ImplementationBehaviour.Transient);
            binder.BindToDefaultImplementation<IDiamondC>(ImplementationBehaviour.Transient);
            binder.BindToDefaultImplementation<IDiamondD>(ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());
            var serviceA = rootScope.Provide<IDiamondA>();

            // D should be different instances when accessed through B or C
            Assert.NotNull(serviceA);
            Assert.NotNull(serviceA.ServiceBProperty.ServiceDProperty);
            Assert.NotNull(serviceA.ServiceCProperty.ServiceDProperty);
            Assert.NotEqual(serviceA.ServiceBProperty.ServiceDProperty, serviceA.ServiceCProperty.ServiceDProperty);
        }

        [Fact]
        public void MultipleDependenciesOfSameType_Singleton_ShareInstance()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<ISharedService>();
            binder.BindToDefaultImplementation<IMultiConsumer>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var consumer = rootScope.Provide<IMultiConsumer>();

            Assert.NotNull(consumer);
            Assert.NotNull(consumer.Service1);
            Assert.NotNull(consumer.Service2);
            Assert.NotNull(consumer.Service3);

            // All three should be the same singleton instance
            Assert.Equal(consumer.Service1, consumer.Service2);
            Assert.Equal(consumer.Service2, consumer.Service3);
        }

        [Fact]
        public void MultipleDependenciesOfSameType_Transient_CreatesMultiple()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<ISharedService>(ImplementationBehaviour.Transient);
            binder.BindToDefaultImplementation<IMultiConsumer>(ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());
            var consumer = rootScope.Provide<IMultiConsumer>();

            Assert.NotNull(consumer);
            Assert.NotNull(consumer.Service1);
            Assert.NotNull(consumer.Service2);
            Assert.NotNull(consumer.Service3);

            // All three should be different transient instances
            Assert.NotEqual(consumer.Service1, consumer.Service2);
            Assert.NotEqual(consumer.Service2, consumer.Service3);
            Assert.NotEqual(consumer.Service1, consumer.Service3);
        }

        [Fact]
        public void MixedBehaviorChain_SingletonDependsOnTransient()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<ISingletonService>();
            binder.BindToDefaultImplementation<ITransientService>(ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());

            // First resolution
            var singleton1 = rootScope.Provide<ISingletonService>();
            var transient1 = singleton1.TransientDependency;

            // Second resolution
            var singleton2 = rootScope.Provide<ISingletonService>();
            var transient2 = singleton2.TransientDependency;

            // Singleton should be same instance
            Assert.Equal(singleton1, singleton2);

            // But the transient dependency captured during singleton creation stays the same
            // (because singleton is only created once)
            Assert.Equal(transient1, transient2);
        }

        [Fact]
        public void WideTreeStructure_MultipleTopLevelDependencies()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IWideService>();
            binder.BindToDefaultImplementation<IServiceA>();
            binder.BindToDefaultImplementation<IServiceB>();
            binder.BindToDefaultImplementation<IServiceC>();
            binder.BindToDefaultImplementation<IDiamondD>();
            binder.BindToDefaultImplementation<IServiceE>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var wideService = rootScope.Provide<IWideService>();

            // Verify all top-level dependencies are resolved
            Assert.NotNull(wideService);
            Assert.NotNull(wideService.ServiceAProperty);
            Assert.NotNull(wideService.ServiceBProperty);
            Assert.NotNull(wideService.ServiceCProperty);
            Assert.NotNull(wideService.ServiceDProperty);
            Assert.NotNull(wideService.ServiceEProperty);
        }

        // Deep chain: A → B → C → D → E
        [DefaultImplementation(typeof(ServiceA))]
        private interface IServiceA
        {
            IServiceB ServiceBProperty { get; }
        }

        [DefaultImplementation(typeof(ServiceB))]
        private interface IServiceB
        {
            IServiceC ServiceCProperty { get; }
        }

        [DefaultImplementation(typeof(ServiceC))]
        private interface IServiceC
        {
            IServiceD ServiceDProperty { get; }
        }

        [DefaultImplementation(typeof(ServiceD))]
        private interface IServiceD
        {
            IServiceE ServiceEProperty { get; }
        }

        [DefaultImplementation(typeof(ServiceE))]
        private interface IServiceE;

        private sealed class ServiceA : IServiceA
        {
            [Inject] public IServiceB ServiceBProperty { get; } = null;
        }

        private sealed class ServiceB : IServiceB
        {
            [Inject] public IServiceC ServiceCProperty { get; } = null;
        }

        private sealed class ServiceC : IServiceC
        {
            [Inject] public IServiceD ServiceDProperty { get; } = null;
        }

        private sealed class ServiceD : IServiceD
        {
            [Inject] public IServiceE ServiceEProperty { get; } = null;
        }

        private sealed class ServiceE : IServiceE;

        // Diamond pattern: A → B,C; B,C → D
        [DefaultImplementation(typeof(DiamondA))]
        private interface IDiamondA
        {
            IDiamondB ServiceBProperty { get; }
            IDiamondC ServiceCProperty { get; }
        }

        [DefaultImplementation(typeof(DiamondB))]
        private interface IDiamondB
        {
            IDiamondD ServiceDProperty { get; }
        }

        [DefaultImplementation(typeof(DiamondC))]
        private interface IDiamondC
        {
            IDiamondD ServiceDProperty { get; }
        }

        [DefaultImplementation(typeof(DiamondD))]
        private interface IDiamondD;

        private sealed class DiamondA : IDiamondA
        {
            [Inject] public IDiamondB ServiceBProperty { get; } = null;
            [Inject] public IDiamondC ServiceCProperty { get; } = null;
        }

        private sealed class DiamondB : IDiamondB
        {
            [Inject] public IDiamondD ServiceDProperty { get; } = null;
        }

        private sealed class DiamondC : IDiamondC
        {
            [Inject] public IDiamondD ServiceDProperty { get; } = null;
        }

        private sealed class DiamondD : IDiamondD;

        // Multiple dependencies of same type
        [DefaultImplementation(typeof(SharedService))]
        private interface ISharedService;

        [DefaultImplementation(typeof(MultiConsumer))]
        private interface IMultiConsumer
        {
            ISharedService Service1 { get; }
            ISharedService Service2 { get; }
            ISharedService Service3 { get; }
        }

        private sealed class SharedService : ISharedService;

        private sealed class MultiConsumer : IMultiConsumer
        {
            [Inject] public ISharedService Service1 { get; } = null;
            [Inject] public ISharedService Service2 { get; } = null;
            [Inject] public ISharedService Service3 { get; } = null;
        }

        // Mixed behaviors
        [DefaultImplementation(typeof(SingletonService))]
        private interface ISingletonService
        {
            ITransientService TransientDependency { get; }
        }

        [DefaultImplementation(typeof(TransientService))]
        private interface ITransientService;

        private sealed class SingletonService : ISingletonService
        {
            [Inject] public ITransientService TransientDependency { get; } = null;
        }

        private sealed class TransientService : ITransientService;

        // Wide tree structure
        [DefaultImplementation(typeof(WideService))]
        private interface IWideService
        {
            IServiceA ServiceAProperty { get; }
            IServiceB ServiceBProperty { get; }
            IServiceC ServiceCProperty { get; }
            IDiamondD ServiceDProperty { get; }
            IServiceE ServiceEProperty { get; }
        }

        private sealed class WideService : IWideService
        {
            [Inject] public IServiceA ServiceAProperty { get; } = null;
            [Inject] public IServiceB ServiceBProperty { get; } = null;
            [Inject] public IServiceC ServiceCProperty { get; } = null;
            [Inject] public IDiamondD ServiceDProperty { get; } = null;
            [Inject] public IServiceE ServiceEProperty { get; } = null;
        }
    }
}