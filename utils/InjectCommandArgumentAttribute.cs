using System;

namespace Core.Utils
{
    [AttributeUsage(AttributeTargets.Field)]
    public class InjectCommandArgumentAttribute : Attribute
    {
        public readonly string ArgumentName;

        public InjectCommandArgumentAttribute(string argumentName = null) =>
            ArgumentName = argumentName;
    }
}