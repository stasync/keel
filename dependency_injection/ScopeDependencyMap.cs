using Keel.DependencyInjection.Factories;
using System;
using System.Collections.Generic;

namespace Keel.DependencyInjection
{
    public sealed class ScopeDependencyMap
    {
        /// <summary>
        /// Key - implementation type.
        /// </summary>
        public IReadOnlyDictionary<Type, InstanceFactory> Factories =>
            _factories;

        /// <summary>
        /// Key - interface type, value - implementation type.
        /// </summary>
        public IReadOnlyDictionary<Type, Type> InterfaceToImplementationMap =>
            _interfaceToImplementationMap;

        /// <summary>
        /// Key - implementation type.
        /// </summary>
        public IReadOnlyDictionary<Type, ImplementationBehaviour> ImplementationBehaviourTypeMap =>
            _implementationBehaviourTypeMap;

        private readonly Dictionary<Type, InstanceFactory> _factories;
        private readonly Dictionary<Type, Type> _interfaceToImplementationMap;
        private readonly Dictionary<Type, ImplementationBehaviour> _implementationBehaviourTypeMap;

        internal ScopeDependencyMap() : this(
            factories: new Dictionary<Type, InstanceFactory>(),
            interfaceToImplementationMap: new Dictionary<Type, Type>(),
            implementationBehaviourTypeMap: new Dictionary<Type, ImplementationBehaviour>())
        {
        }

        internal ScopeDependencyMap(Dictionary<Type, InstanceFactory> factories, Dictionary<Type, Type> interfaceToImplementationMap, Dictionary<Type, ImplementationBehaviour> implementationBehaviourTypeMap)
        {
            _factories = factories;
            _interfaceToImplementationMap = interfaceToImplementationMap;
            _implementationBehaviourTypeMap = implementationBehaviourTypeMap;
        }
    }
}