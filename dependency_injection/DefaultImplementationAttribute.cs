using System;

namespace Keel.DependencyInjection
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
    public class DefaultImplementationAttribute : Attribute
    {
        public readonly Type Implementation;
        public readonly Type FactoryType;

        public DefaultImplementationAttribute(Type implementation, Type factoryOverride = null)
        {
            Implementation = implementation;
            FactoryType = factoryOverride;
        }
    }
}