namespace Core.DependencyInjection.Tests
{
    public class NestedScopesTests
    {
        [Fact]
        public void BasicNestedScopeSetup()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            var childScopeBinder = new ScopeBinder();
            childScopeBinder.BindToDefaultImplementation<ISecondService>();
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            // Root scope
            {
                // First service.
                {
                    var firstService = rootScope.Provide<IFirstService>();
                    Assert.NotNull(firstService);
                    Assert.IsAssignableFrom<FirstService>(firstService);

                    // Check if scope was correctly injected.
                    Assert.NotNull(firstService.Scope);
                    Assert.True(firstService.Scope == rootScope);
                }

                // Second service.
                {
                    // Second service is not a part of the root scope.
                    var secondServiceShouldBeNull = rootScope.Provide<ISecondService>();
                    Assert.Null(secondServiceShouldBeNull);
                }
            }

            // Child scope - both services should be available here.
            {
                // First service.
                {
                    var secondService = childScope.Provide<ISecondService>();
                    Assert.NotNull(secondService);
                    Assert.IsAssignableFrom<SecondService>(secondService);

                    // Check if scope was correctly injected.
                    Assert.NotNull(secondService.Scope);
                    Assert.True(secondService.Scope == childScope);

                    // Check the reference. It will be taken form the root scope,
                    Assert.NotNull(secondService.FirstServiceRef);
                    Assert.IsAssignableFrom<FirstService>(secondService.FirstServiceRef);
                }

                // Second service.
                {
                    var firstService = childScope.Provide<IFirstService>();
                    Assert.NotNull(firstService);
                    Assert.IsAssignableFrom<FirstService>(firstService);

                    // Check if scope was correctly injected.
                    Assert.NotNull(firstService.Scope);
                    Assert.True(firstService.Scope == rootScope);
                }
            }
        }

        [Fact]
        public void NestedScopeOverrides()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            // In child scope we want to override IFirstService implementation from the root scope.
            // Overriden implementation will a part of the child scope.
            var childScopeBinder = new ScopeBinder();
            childScopeBinder.BindToDefaultImplementation<ISecondService>();
            childScopeBinder.Bind<IFirstService, CustomFirstService>();
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            // Root scope
            {
                // First service.
                {
                    var firstService = rootScope.Provide<IFirstService>();
                    Assert.NotNull(firstService);
                    Assert.IsAssignableFrom<FirstService>(firstService);

                    // Check if scope was correctly injected.
                    Assert.NotNull(firstService.Scope);
                    Assert.True(firstService.Scope == rootScope);
                }

                // Second service.
                {
                    // Second service is not a part of the root scope.
                    var secondServiceShouldBeNull = rootScope.Provide<ISecondService>();
                    Assert.Null(secondServiceShouldBeNull);
                }
            }

            // Child scope - both services should be available here.
            {
                // Second service.
                {
                    var secondService = childScope.Provide<ISecondService>();
                    Assert.NotNull(secondService);
                    Assert.IsAssignableFrom<SecondService>(secondService);

                    // Check if scope was correctly injected.
                    Assert.NotNull(secondService.Scope);
                    Assert.True(secondService.Scope == childScope);

                    // Check the reference.
                    Assert.NotNull(secondService.FirstServiceRef);
                    Assert.IsAssignableFrom<CustomFirstService>(secondService.FirstServiceRef);
                }

                // First service.
                {
                    var firstService = childScope.Provide<IFirstService>();
                    Assert.NotNull(firstService);
                    Assert.IsAssignableFrom<CustomFirstService>(firstService);

                    // Check if scope was correctly injected.
                    // Has to belong to the child scope, as we override it.
                    Assert.NotNull(firstService.Scope);
                    Assert.True(firstService.Scope == childScope);
                }
            }
        }

        private sealed class FirstService : IFirstService
        {
            [Inject]
            public Scope Scope { get; } = null;
        }

        private sealed class SecondService : ISecondService
        {
            [Inject]
            public Scope Scope { get; } = null;

            [Inject]
            public IFirstService FirstServiceRef { get; } = null;
        }

        private sealed class CustomFirstService : IFirstService
        {
            [Inject]
            public Scope Scope { get; } = null;
        }

        [DefaultImplementation(typeof(FirstService))]
        private interface IFirstService
        {
            Scope Scope { get; }
        }

        [DefaultImplementation(typeof(SecondService))]
        private interface ISecondService
        {
            Scope Scope { get; }

            IFirstService FirstServiceRef { get; }
        }

        [Fact]
        public void DeepNesting_ThreeLevels_ResolvesCorrectly()
        {
            // Setup: Root -> Child -> GrandChild
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            var childScopeBinder = new ScopeBinder();
            childScopeBinder.BindToDefaultImplementation<ISecondService>();
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            var grandChildScopeBinder = new ScopeBinder();
            grandChildScopeBinder.BindToDefaultImplementation<IThirdService>();
            var grandChildScope = childScope.CreateChildScope(grandChildScopeBinder.ToDependencyMap());

            // GrandChild should have access to all three services
            var thirdService = grandChildScope.Provide<IThirdService>();
            Assert.NotNull(thirdService);
            Assert.IsAssignableFrom<ThirdService>(thirdService);
            Assert.Equal(grandChildScope, thirdService.Scope);

            // Should be able to resolve services from parent and grandparent
            var secondService = grandChildScope.Provide<ISecondService>();
            Assert.NotNull(secondService);
            Assert.Equal(childScope, secondService.Scope);

            var firstService = grandChildScope.Provide<IFirstService>();
            Assert.NotNull(firstService);
            Assert.Equal(rootScope, firstService.Scope);

            // ThirdService should have properly resolved dependencies from ancestors
            Assert.NotNull(thirdService.SecondServiceRef);
            Assert.NotNull(thirdService.SecondServiceRef.FirstServiceRef);
        }

        [Fact]
        public void TransientBehaviorInNestedScope_CreatesNewInstances()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.Bind<IFirstService, FirstService>(ImplementationBehaviour.Transient);
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            var childScopeBinder = new ScopeBinder();
            childScopeBinder.Bind<ISecondService, TransientSecondService>(ImplementationBehaviour.Transient);
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            // Each resolve should create a new instance
            var service1 = childScope.Provide<ISecondService>();
            var service2 = childScope.Provide<ISecondService>();

            Assert.NotNull(service1);
            Assert.NotNull(service2);
            Assert.NotEqual(service1, service2);

            // Dependencies should also be transient
            Assert.NotEqual(service1.FirstServiceRef, service2.FirstServiceRef);
        }

        [Fact]
        public void SiblingScopes_DontShareSingletonInstances()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            // Create two sibling child scopes with the same bindings
            var childScopeBinder1 = new ScopeBinder();
            childScopeBinder1.BindToDefaultImplementation<ISecondService>();
            var childScope1 = rootScope.CreateChildScope(childScopeBinder1.ToDependencyMap());

            var childScopeBinder2 = new ScopeBinder();
            childScopeBinder2.BindToDefaultImplementation<ISecondService>();
            var childScope2 = rootScope.CreateChildScope(childScopeBinder2.ToDependencyMap());

            // Get second service from both siblings
            var service1 = childScope1.Provide<ISecondService>();
            var service2 = childScope2.Provide<ISecondService>();

            // Sibling scopes should have separate singleton instances
            Assert.NotNull(service1);
            Assert.NotNull(service2);
            Assert.NotEqual(service1, service2);

            // But they should share the root scope singleton
            Assert.Equal(service1.FirstServiceRef, service2.FirstServiceRef);

            // Verify each belongs to its respective scope
            Assert.Equal(childScope1, service1.Scope);
            Assert.Equal(childScope2, service2.Scope);
        }

        [Fact]
        public void ParentScopeDisposal_DisposesChildScopes()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            var childScopeBinder = new ScopeBinder();
            childScopeBinder.BindToDefaultImplementation<ISecondService>();
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            var grandChildScopeBinder = new ScopeBinder();
            grandChildScopeBinder.BindToDefaultImplementation<IThirdService>();
            var grandChildScope = childScope.CreateChildScope(grandChildScopeBinder.ToDependencyMap());

            // Dispose parent scope
            rootScope.Dispose();

            // All child scopes should be disposed
            Assert.Throws<ObjectDisposedException>(() => rootScope.Provide<IFirstService>());
            Assert.Throws<ObjectDisposedException>(() => childScope.Provide<ISecondService>());
            Assert.Throws<ObjectDisposedException>(() => grandChildScope.Provide<IThirdService>());
        }

        [Fact]
        public void OverrideWithTransient_ParentSingleton_ChildTransient()
        {
            var rootScopeBinder = new ScopeBinder();
            rootScopeBinder.Bind<IFirstService, FirstService>();
            var rootScope = new RootScope(rootScopeBinder.ToDependencyMap());

            // Child overrides with transient behavior
            var childScopeBinder = new ScopeBinder();
            childScopeBinder.Bind<IFirstService, CustomFirstService>(ImplementationBehaviour.Transient);
            var childScope = rootScope.CreateChildScope(childScopeBinder.ToDependencyMap());

            // Parent should have singleton behavior
            var rootService1 = rootScope.Provide<IFirstService>();
            var rootService2 = rootScope.Provide<IFirstService>();
            Assert.Equal(rootService1, rootService2);

            // Child should have transient behavior
            var childService1 = childScope.Provide<IFirstService>();
            var childService2 = childScope.Provide<IFirstService>();
            Assert.NotEqual(childService1, childService2);

            // Both should be CustomFirstService type
            Assert.IsAssignableFrom<CustomFirstService>(childService1);
            Assert.IsAssignableFrom<CustomFirstService>(childService2);

            // Should not be the same as parent instances
            Assert.IsAssignableFrom<FirstService>(rootService1);
            Assert.NotEqual(rootService1, childService1);
        }

        private sealed class ThirdService : IThirdService
        {
            [Inject]
            public Scope Scope { get; } = null;

            [Inject]
            public ISecondService SecondServiceRef { get; } = null;
        }

        private sealed class TransientSecondService : ISecondService
        {
            [Inject]
            public Scope Scope { get; } = null;

            [Inject]
            public IFirstService FirstServiceRef { get; } = null;
        }

        [DefaultImplementation(typeof(ThirdService))]
        private interface IThirdService
        {
            Scope Scope { get; }

            ISecondService SecondServiceRef { get; }
        }
    }
}