using Core.DependencyInjection.Internal;
using System;
using System.Collections.Generic;

namespace Core.DependencyInjection.Diagnostics
{
    public static class CircularDependencyDetector
    {
        public static void Validate(ScopeDependencyMap dependencyMap)
        {
            foreach (var implementationType in dependencyMap.InterfaceToImplementationMap.Values)
                ValidateRecursive(implementationType, dependencyMap, visitedTypes: new HashSet<Type>(), resolutionPath: string.Empty);
        }

        private static void ValidateRecursive(Type currentImplementationType, ScopeDependencyMap dependencyMap, HashSet<Type> visitedTypes, string resolutionPath)
        {
            resolutionPath = $"{resolutionPath}/{currentImplementationType.FullName}";

            if (!visitedTypes.Add(currentImplementationType))
                throw new InvalidOperationException($"Circular dependency detected at '{resolutionPath}'.");

            foreach (var field in InjectionTargetsCache.GetFields(currentImplementationType))
            {
                var dependencyType = field.FieldType;
                if (dependencyMap.InterfaceToImplementationMap.TryGetValue(dependencyType, out var implementationType))
                    ValidateRecursive(implementationType, dependencyMap, visitedTypes, resolutionPath);
            }

            visitedTypes.Remove(currentImplementationType);
        }
    }
}