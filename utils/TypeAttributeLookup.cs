using System;
using System.Collections.Generic;

namespace Keel.Utils
{
    public static class TypeAttributeLookup<TAttribute> where TAttribute : Attribute
    {
        public readonly struct LookupData
        {
            public readonly Type Type;
            public readonly TAttribute AttributeData;

            public LookupData(Type type, TAttribute attributeData)
            {
                Type = type;
                AttributeData = attributeData;
            }
        }

        public static IReadOnlyList<LookupData> Value => s_value;
        private static readonly List<LookupData> s_value = new();

        static TypeAttributeLookup()
        {
            var attributeType = typeof(TAttribute);
            foreach (var type in Types.AllTracked)
            {
                if (Attribute.GetCustomAttribute(type, attributeType) is TAttribute attribute)
                    s_value.Add(new LookupData(type, attribute));
            }
        }
    }
}