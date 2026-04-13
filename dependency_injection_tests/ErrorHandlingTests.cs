#pragma warning disable CS0414 // Field is assigned but its value is never used
using Core.DependencyInjection.Interface;

namespace Core.DependencyInjection.Tests
{
    public class ErrorHandlingTests
    {
        [Fact]
        public void ProvideUnregisteredInterface_ReturnsNull()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            // Request interface that was never bound
            var unregisteredService = rootScope.Provide<ISecondService>();
            Assert.Null(unregisteredService);
        }

        [Fact]
        public void DuplicateBinding_ThrowsInvalidOperationException()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();

            // Attempting to bind the same interface again should throw
            Assert.Throws<InvalidOperationException>(() =>
                binder.Bind<IFirstService, AlternativeFirstService>());
        }

        [Fact]
        public void BindingConflictingBehaviors_ThrowsInvalidOperationException()
        {
            var binder = new ScopeBinder();
            binder.Bind<IFirstService, FirstService>();

            // Same interface, same implementation, but different behavior should throw
            Assert.Throws<InvalidOperationException>(() =>
                binder.Bind<IFirstService, FirstService>(ImplementationBehaviour.Transient));
        }

        [Fact]
        public void ProvideAfterDisposal_ThrowsObjectDisposedException()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            // Dispose the scope
            rootScope.Dispose();

            // Attempting to provide after disposal should throw
            Assert.Throws<ObjectDisposedException>(() => rootScope.Provide<IFirstService>());
        }

        [Fact]
        public void BindInvalidImplementationType_ThrowsInvalidOperationException()
        {
            var binder = new ScopeBinder();

            // Attempting to bind an interface as implementation should throw
            Assert.Throws<InvalidOperationException>(() =>
                binder.Bind<IFirstService, ISecondService>());
        }

        private sealed class FirstService : IFirstService;

        private sealed class AlternativeFirstService : IFirstService;

        private sealed class RecursiveService : IRecursiveService
        {
            [Inject] private readonly Scope _scope = null;

            [Inject] private IFirstService _firstService = null;

            public RecursiveService()
            {
                // This will be null during injection phase, but we trigger in OnResolved
            }

            public void OnResolved()
            {
                // This should throw - can't call Provide during resolution
                _scope.Provide<ISecondService>();
            }
        }

        [DefaultImplementation(typeof(FirstService))]
        private interface IFirstService;

        private interface ISecondService : IFirstService;

        [DefaultImplementation(typeof(RecursiveService))]
        private interface IRecursiveService : IScopeListener;
    }
}