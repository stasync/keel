using Keel.DependencyInjection.Factories;

namespace Keel.DependencyInjection.Tests
{
    public class CustomFactoryTests
    {
        [Fact]
        public void Singleton()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService, CustomFactory>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var fistService = rootScope.Provide<IFirstService>();
            Assert.NotNull(fistService);
        }

        [Fact]
        public void Transient()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService, CustomFactory>(ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());
            var fistService = rootScope.Provide<IFirstService>();
            Assert.NotNull(fistService);
        }

        private sealed class FirstService : IFirstService;

        private interface IFirstService;

        private sealed class CustomFactory : InstanceFactory
        {
            public CustomFactory(Type implementationType) : base(implementationType) { }

            public override object Produce() =>
                Activator.CreateInstance(ImplementationType);
        }
    }
}