namespace Core.DependencyInjection.Tests
{
    public class DefaultImplementationTests
    {
        [Fact]
        public void Test()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            binder.BindToDefaultImplementation<ISecondService>();

            var rootScope = new RootScope(binder.ToDependencyMap());

            // First service
            {
                var firstService = rootScope.Provide<IFirstService>();
                Assert.NotNull(firstService);
                firstService.Test();
            }

            // Second service
            {
                var secondService = rootScope.Provide<ISecondService>();
                Assert.NotNull(secondService);
                secondService.Test();
            }
        }

        private sealed class FirstService : IFirstService
        {
            [Inject]
            public ISecondService SecondServiceRef { get; } = null;

            public void Test()
            {
                Assert.NotNull(SecondServiceRef);
                Assert.IsAssignableFrom<SecondService>(SecondServiceRef);
            }
        }

        private sealed class SecondService : ISecondService
        {
            public void Test()
            {
            }
        }

        [DefaultImplementation(typeof(FirstService))]
        private interface IFirstService
        {
            ISecondService SecondServiceRef { get; }

            void Test();
        }

        [DefaultImplementation(typeof(SecondService))]
        private interface ISecondService
        {
            void Test();
        }
    }
}