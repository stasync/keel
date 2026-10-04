namespace Keel.DependencyInjection.Tests
{
    public class ProxyFactoryTest
    {
        [Fact]
        public void Singleton()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>(instanceProvider: () => new FirstService());

            var rootScope = new RootScope(binder.ToDependencyMap());
            var fistService = rootScope.Provide<IFirstService>();
            Assert.NotNull(fistService);
        }

        [Fact]
        public void Transient()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>(instanceProvider: () => new FirstService(), ImplementationBehaviour.Transient);

            var rootScope = new RootScope(binder.ToDependencyMap());
            var fistService = rootScope.Provide<IFirstService>();
            Assert.NotNull(fistService);
        }

        private sealed class FirstService : IFirstService;

        private interface IFirstService;
    }
}