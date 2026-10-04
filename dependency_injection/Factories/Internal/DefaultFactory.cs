using System;

namespace Keel.DependencyInjection.Factories.Internal
{
    internal sealed class DefaultFactory : InstanceFactory
    {
        public DefaultFactory(Type implementationType) : base(implementationType) { }

        public override object Produce() =>
            Activator.CreateInstance(ImplementationType);
    }
}