namespace Core.DependencyInjection.Tests
{
    public class ManualBindingTests
    {
        [Fact]
        public void Test()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();
            binder.Bind<ISecondService, SecondService>();
            binder.Bind<IThirdService, ThirdService>();

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

            // Third service
            {
                var thirdService = rootScope.Provide<IThirdService>();
                Assert.NotNull(thirdService);
                thirdService.Test();
            }
        }

        private sealed class FirstService : IFirstService
        {
            [Inject]
            private readonly ISecondService _secondServiceRef = null;

            public void Test()
            {
                Assert.NotNull(_secondServiceRef);
                Assert.IsAssignableFrom<SecondService>(_secondServiceRef);
                _secondServiceRef.Test();
            }
        }

        private sealed class SecondService : ISecondService
        {
            public void Test()
            {
            }
        }

        private sealed class ThirdService : IThirdService
        {
            [Inject]
            private readonly IFirstService _firstService = null;

            [Inject]
            private readonly ISecondService _secondServiceRef = null;

            public void Test()
            {
                Assert.NotNull(_firstService);
                Assert.IsAssignableFrom<FirstService>(_firstService);

                Assert.NotNull(_secondServiceRef);
                Assert.IsAssignableFrom<SecondService>(_secondServiceRef);
            }
        }

        private interface IFirstService
        {
            void Test();
        }

        private interface ISecondService
        {
            void Test();
        }

        private interface IThirdService
        {
            void Test();
        }
    }
}