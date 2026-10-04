using System;
using System.Collections.Generic;

namespace Keel.DependencyInjection.Internal
{
    internal sealed class DependencyResolvingContext : IDisposable
    {
        private readonly List<object> _instances = new();

        internal IReadOnlyList<object> GetAllContextInstances() =>
            _instances;

        internal void AddNewInstance(object instance) =>
            _instances.Add(instance);

        void IDisposable.Dispose() =>
            _instances.Clear();
    }
}