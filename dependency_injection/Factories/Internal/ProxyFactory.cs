using System;

namespace Core.DependencyInjection.Factories.Internal
{
    internal sealed class ProxyFactory<TImplementation> : InstanceFactory where TImplementation : class
    {
        private readonly Func<TImplementation> _producer;

        public ProxyFactory(Func<TImplementation> producer) : base(typeof(TImplementation)) =>
            _producer = producer;

        public override object Produce() =>
            _producer();
    }
}