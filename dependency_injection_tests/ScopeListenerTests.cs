using Keel.DependencyInjection.Interface;

namespace Keel.DependencyInjection.Tests
{
    public class ScopeListenerTests
    {
        [Fact]
        public void Test()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();
            binder.Bind<ISecondService, SecondService>();
            binder.Bind<IThirdService, ThirdService>();
            binder.Bind<IForthService, ForthService>();

            var rootScope = new RootScope(binder.ToDependencyMap());
            var firstService = rootScope.Provide<IFirstService>();
            var secondService = rootScope.Provide<ISecondService>();
            var thirdService = rootScope.Provide<IThirdService>();
            var forthService = rootScope.Provide<IForthService>();

            Assert.NotNull(firstService);
            Assert.NotNull(secondService);
            Assert.NotNull(thirdService);
            Assert.NotNull(forthService);

            Assert.Equal(1, firstService.OnResolvedCalledCnt);
            Assert.Equal(1, secondService.OnResolvedCalledCnt);
            Assert.Equal(1, thirdService.OnResolvedCalledCnt);
            Assert.Equal(1, forthService.OnResolvedCalledCnt);

            Assert.NotNull(firstService.SecondServiceRef);
            Assert.NotNull(secondService.ForthServiceRef);

            Assert.NotNull(thirdService.FirstServiceRef);
            Assert.NotNull(thirdService.SecondServiceRef);
        }

        private sealed class FirstService : IFirstService
        {
            public int OnResolvedCalledCnt { get; private set; }
            [Inject] public ISecondService SecondServiceRef { get; }

            public void OnResolved()
            {
                OnResolvedCalledCnt++;
            }
        }

        private sealed class SecondService : ISecondService
        {
            public int OnResolvedCalledCnt { get; private set; }
            [Inject] public IForthService ForthServiceRef { get; }

            public void OnResolved()
            {
                OnResolvedCalledCnt++;
            }
        }

        private sealed class ThirdService : IThirdService
        {
            public int OnResolvedCalledCnt { get; private set; }
            [Inject] public IFirstService FirstServiceRef { get; }
            public ISecondService SecondServiceRef { get; private set; }

            [Inject] private readonly Scope _scope = null;

            public void OnResolved()
            {
                OnResolvedCalledCnt++;
                SecondServiceRef = _scope.Provide<ISecondService>();
            }
        }

        private sealed class ForthService : IForthService
        {
            public int OnResolvedCalledCnt { get; private set; }
            public void OnResolved()
            {
                OnResolvedCalledCnt++;
            }
        }

        private interface IFirstService : IScopeListener
        {
            ISecondService SecondServiceRef { get; }
            int OnResolvedCalledCnt { get; }
        }

        private interface ISecondService : IScopeListener
        {
            IForthService ForthServiceRef { get; }
            int OnResolvedCalledCnt { get; }
        }

        private interface IThirdService : IScopeListener
        {
            IFirstService FirstServiceRef { get; }
            ISecondService SecondServiceRef { get; }
            int OnResolvedCalledCnt { get; }
        }

        private interface IForthService : IScopeListener
        {
            int OnResolvedCalledCnt { get; }
        }
    }
}