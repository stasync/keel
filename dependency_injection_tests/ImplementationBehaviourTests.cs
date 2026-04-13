#pragma warning disable CS0169 // Field is never used
namespace Core.DependencyInjection.Tests
{
    public class ImplementationBehaviourTests
    {
        [Fact]
        public void Singleton()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();
            binder.Bind<ISecondService, SecondService>();
            var rootScope = new RootScope(binder.ToDependencyMap());
            var firstService0 = rootScope.Provide<IFirstService>();
            Assert.NotNull(firstService0);
            Assert.NotNull(firstService0.SecondServiceRef0);
            Assert.NotNull(firstService0.SecondServiceRef1);
            Assert.Equal(firstService0.SecondServiceRef0, firstService0.SecondServiceRef1);

            var firstService1 = rootScope.Provide<IFirstService>();
            Assert.NotNull(firstService0);
            Assert.Equal(firstService0, firstService1);
        }

        [Fact]
        public void Transient()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>(ImplementationBehaviour.Transient);
            binder.Bind<ISecondService, SecondService>(ImplementationBehaviour.Transient);
            var rootScope = new RootScope(binder.ToDependencyMap());
            var firstService0 = rootScope.Provide<IFirstService>();
            Assert.NotNull(firstService0);
            Assert.NotNull(firstService0.SecondServiceRef0);
            Assert.NotNull(firstService0.SecondServiceRef1);
            Assert.NotEqual(firstService0.SecondServiceRef0, firstService0.SecondServiceRef1);

            var firstService1 = rootScope.Provide<IFirstService>();
            Assert.NotNull(firstService0);
            Assert.NotEqual(firstService0, firstService1);
        }

        private sealed class FirstService : IFirstService
        {
            [Inject] public ISecondService SecondServiceRef0 { get; } = null;
            [Inject] public ISecondService SecondServiceRef1 { get; } = null;
        }

        private sealed class SecondService : ISecondService;

        private interface IFirstService
        {
            ISecondService SecondServiceRef0 { get; }
            ISecondService SecondServiceRef1 { get; }
        }

        private interface ISecondService;

        [Fact]
        public void MixedBehavior_TransientDependsOnSingleton()
        {
            var binder = new ScopeBinder();
            binder.Bind<ITransientRoot, TransientRoot>(ImplementationBehaviour.Transient);
            binder.Bind<ISingletonDep, SingletonDep>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            var transient1 = rootScope.Provide<ITransientRoot>();
            var transient2 = rootScope.Provide<ITransientRoot>();

            // Transient instances should be different
            Assert.NotEqual(transient1, transient2);

            // But their singleton dependency should be the same
            Assert.Equal(transient1.SingletonDependencyProperty, transient2.SingletonDependencyProperty);
        }

        [Fact]
        public void MixedBehavior_SingletonDependsOnTransient_CapturesOnce()
        {
            var binder = new ScopeBinder();
            binder.Bind<ISingletonRoot, SingletonRoot>();
            binder.Bind<ITransientDep, TransientDep>(ImplementationBehaviour.Transient);
            var rootScope = new RootScope(binder.ToDependencyMap());

            var singleton1 = rootScope.Provide<ISingletonRoot>();
            var singleton2 = rootScope.Provide<ISingletonRoot>();

            // Singleton instances should be the same
            Assert.Equal(singleton1, singleton2);

            // The transient captured during singleton creation stays the same
            Assert.Equal(singleton1.TransientDependencyProperty, singleton2.TransientDependencyProperty);

            // Direct resolve of transient should be different
            var directTransient = rootScope.Provide<ITransientDep>();
            Assert.NotEqual(singleton1.TransientDependencyProperty, directTransient);
        }

        [Fact]
        public void ComplexMixedBehavior_MultipleTransientsSingletonRoot()
        {
            var binder = new ScopeBinder();
            binder.Bind<IComplexRoot, ComplexRoot>();
            binder.Bind<IFirstTransient, FirstTransient>(ImplementationBehaviour.Transient);
            binder.Bind<ISecondTransient, SecondTransient>(ImplementationBehaviour.Transient);
            var rootScope = new RootScope(binder.ToDependencyMap());

            var root1 = rootScope.Provide<IComplexRoot>();
            var root2 = rootScope.Provide<IComplexRoot>();

            // Root should be singleton
            Assert.Equal(root1, root2);

            // All transient dependencies captured should be the same (captured once)
            Assert.Equal(root1.FirstTransientProperty, root2.FirstTransientProperty);
            Assert.Equal(root1.SecondTransientProperty, root2.SecondTransientProperty);

            // But FirstTransient and SecondTransient should be different instances
            Assert.NotEqual(root1.FirstTransientProperty, (object)root1.SecondTransientProperty);
        }

        private sealed class TransientRoot : ITransientRoot
        {
            [Inject] public ISingletonDep SingletonDependencyProperty { get; } = null;
        }

        private sealed class SingletonDep : ISingletonDep;

        private sealed class SingletonRoot : ISingletonRoot
        {
            [Inject] public ITransientDep TransientDependencyProperty { get; } = null;
        }

        private sealed class TransientDep : ITransientDep;

        private sealed class ComplexRoot : IComplexRoot
        {
            [Inject] public IFirstTransient FirstTransientProperty { get; } = null;
            [Inject] public ISecondTransient SecondTransientProperty { get; } = null;
        }

        private sealed class FirstTransient : IFirstTransient;
        private sealed class SecondTransient : ISecondTransient;

        private interface ITransientRoot
        {
            ISingletonDep SingletonDependencyProperty { get; }
        }

        private interface ISingletonDep;

        private interface ISingletonRoot
        {
            ITransientDep TransientDependencyProperty { get; }
        }

        private interface ITransientDep;

        private interface IComplexRoot
        {
            IFirstTransient FirstTransientProperty { get; }
            ISecondTransient SecondTransientProperty { get; }
        }

        private interface IFirstTransient;
        private interface ISecondTransient;
    }
}