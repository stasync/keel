using System;

namespace Core.DependencyInjection.Factories
{
    public abstract class InstanceFactory
    {
        protected readonly Type ImplementationType;

        protected InstanceFactory(Type implementationType) =>
            ImplementationType = implementationType;

        public abstract object Produce();
    }
}