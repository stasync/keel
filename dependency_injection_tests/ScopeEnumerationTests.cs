namespace Core.DependencyInjection.Tests
{
    public class ScopeEnumerationTests
    {
        [Fact]
        public void EnumerateActiveInstances_EmptyScope_ReturnsEmpty()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            var instances = rootScope.EnumerateActiveInstances().ToList();
            Assert.Empty(instances);
        }

        [Fact]
        public void EnumerateActiveInstances_AfterProvide_ReturnsInstance()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            var service = rootScope.Provide<IFirstService>();
            var instances = rootScope.EnumerateActiveInstances().ToList();

            Assert.Single(instances);
            Assert.Equal(service, instances[0]);
        }

        [Fact]
        public void EnumerateActiveInstances_MultipleSingletons_ReturnsAll()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            binder.BindToDefaultImplementation<ISecondService>();
            binder.BindToDefaultImplementation<IThirdService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            var first = rootScope.Provide<IFirstService>();
            var second = rootScope.Provide<ISecondService>();
            var third = rootScope.Provide<IThirdService>();

            var instances = rootScope.EnumerateActiveInstances().ToList();

            Assert.Equal(3, instances.Count);
            Assert.Contains(first, instances);
            Assert.Contains(second, instances);
            Assert.Contains(third, instances);
        }

        [Fact]
        public void EnumerateActiveInstances_TransientNotCached_OnlyDependencies()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<ISingletonWithTransient>();
            binder.BindToDefaultImplementation<ITransientService>(ImplementationBehaviour.Transient);
            var rootScope = new RootScope(binder.ToDependencyMap());

            var singleton = rootScope.Provide<ISingletonWithTransient>();
            var instances = rootScope.EnumerateActiveInstances().ToList();

            // Only singleton should be cached, transients are not stored
            Assert.Single(instances);
            Assert.Equal(singleton, instances[0]);
        }

        [Fact]
        public void EnumerateChildScopes_NoChildren_ReturnsEmpty()
        {
            var binder = new ScopeBinder();
            var rootScope = new RootScope(binder.ToDependencyMap());

            var childScopes = rootScope.ToList();
            Assert.Empty(childScopes);
        }

        [Fact]
        public void EnumerateChildScopes_WithChildren_ReturnsAll()
        {
            var rootBinder = new ScopeBinder();
            var rootScope = new RootScope(rootBinder.ToDependencyMap());

            var childBinder1 = new ScopeBinder();
            var childScope1 = rootScope.CreateChildScope(childBinder1.ToDependencyMap());

            var childBinder2 = new ScopeBinder();
            var childScope2 = rootScope.CreateChildScope(childBinder2.ToDependencyMap());

            var childScopes = rootScope.ToList();

            Assert.Equal(2, childScopes.Count);
            Assert.Contains(childScope1, childScopes);
            Assert.Contains(childScope2, childScopes);
        }

        [Fact]
        public void EnumerateChildScopes_Nested_OnlyDirectChildren()
        {
            var rootBinder = new ScopeBinder();
            var rootScope = new RootScope(rootBinder.ToDependencyMap());

            var childBinder = new ScopeBinder();
            var childScope = rootScope.CreateChildScope(childBinder.ToDependencyMap());

            var grandChildBinder = new ScopeBinder();
            var grandChildScope = childScope.CreateChildScope(grandChildBinder.ToDependencyMap());

            var rootChildren = rootScope.ToList();
            var childChildren = childScope.ToList();

            // Root should only have direct child, not grandchild
            Assert.Single(rootChildren);
            Assert.Contains(childScope, rootChildren);
            Assert.DoesNotContain(grandChildScope, rootChildren);

            // Child should have grandchild
            Assert.Single(childChildren);
            Assert.Contains(grandChildScope, childChildren);
        }

        [Fact]
        public void EnumerateActiveInstances_WithDependencies_ReturnsAllResolved()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            binder.BindToDefaultImplementation<ISecondService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            // FirstService depends on SecondService
            var first = rootScope.Provide<IFirstService>();

            var instances = rootScope.EnumerateActiveInstances().ToList();

            // Both FirstService and SecondService should be in active instances
            Assert.Equal(2, instances.Count);
            Assert.Contains(first, instances);
            Assert.Contains(first.SecondServiceProperty, instances);
        }

        [Fact]
        public void EnumerateActiveInstances_AfterDisposal_Throws()
        {
            var binder = new ScopeBinder();
            binder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(binder.ToDependencyMap());

            rootScope.Provide<IFirstService>();
            rootScope.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                rootScope.EnumerateActiveInstances().ToList());
        }

        [Fact]
        public void ChildScopeInstances_IsolatedFromParent()
        {
            var rootBinder = new ScopeBinder();
            rootBinder.BindToDefaultImplementation<IFirstService>();
            var rootScope = new RootScope(rootBinder.ToDependencyMap());

            var childBinder = new ScopeBinder();
            childBinder.BindToDefaultImplementation<ISecondService>();
            var childScope = rootScope.CreateChildScope(childBinder.ToDependencyMap());

            var firstFromRoot = rootScope.Provide<IFirstService>();
            var secondFromChild = childScope.Provide<ISecondService>();

            var rootInstances = rootScope.EnumerateActiveInstances().ToList();
            var childInstances = childScope.EnumerateActiveInstances().ToList();

            // Root scope should only have its own instance
            Assert.Single(rootInstances);
            Assert.Contains(firstFromRoot, rootInstances);
            Assert.DoesNotContain(secondFromChild, rootInstances);

            // Child scope should only have its own instance
            Assert.Single(childInstances);
            Assert.Contains(secondFromChild, childInstances);
            Assert.DoesNotContain(firstFromRoot, childInstances);
        }

        private sealed class FirstService : IFirstService
        {
            [Inject]
            public ISecondService SecondServiceProperty { get; } = null;
        }

        private sealed class SecondService : ISecondService;

        private sealed class ThirdService : IThirdService;

        private sealed class SingletonWithTransient : ISingletonWithTransient
        {
            [Inject]
            public ITransientService Transient { get; } = null;
        }

        private sealed class TransientService : ITransientService;

        [DefaultImplementation(typeof(FirstService))]
        private interface IFirstService
        {
            ISecondService SecondServiceProperty { get; }
        }

        [DefaultImplementation(typeof(SecondService))]
        private interface ISecondService;

        [DefaultImplementation(typeof(ThirdService))]
        private interface IThirdService;

        [DefaultImplementation(typeof(SingletonWithTransient))]
        private interface ISingletonWithTransient
        {
            ITransientService Transient { get; }
        }

        [DefaultImplementation(typeof(TransientService))]
        private interface ITransientService;
    }
}