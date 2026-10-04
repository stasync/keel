#pragma warning disable CS0169 // Field is never used
namespace Keel.DependencyInjection.Tests
{
    public class CircularReferenceTests
    {
        [Fact]
        public void Singleton()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();
            binder.Bind<ISecondService, SecondService>();
            binder.Bind<IThirdService, ThirdService>();
            Assert.Throws<InvalidOperationException>(() => new RootScope(binder.ToDependencyMap()));
        }

        [Fact]
        public void Transient()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>(ImplementationBehaviour.Transient);
            binder.Bind<ISecondService, SecondService>(ImplementationBehaviour.Transient);
            Assert.Throws<InvalidOperationException>(() => new RootScope(binder.ToDependencyMap()));
        }

        private sealed class FirstService : IFirstService
        {
            [Inject] private ISecondService _secondServiceRef;
            [Inject] private IFirstService _firstServiceRef;
        }

        private sealed class SecondService : ISecondService
        {
            [Inject] private IFirstService _firstServiceRef;
        }

        private sealed class ThirdService : IThirdService
        {
            [Inject] private IFirstService _firstServiceRef;
            [Inject] private ISecondService _secondServiceRef;
        }

        private interface IFirstService;

        private interface ISecondService;

        private interface IThirdService;
    }
}