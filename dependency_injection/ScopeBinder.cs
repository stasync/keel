using Core.DependencyInjection.Factories;
using Core.DependencyInjection.Factories.Internal;
using System;
using System.Collections.Generic;

namespace Core.DependencyInjection
{
    public sealed class ScopeBinder
    {
        // Key - implementation type;
        private readonly Dictionary<Type, InstanceFactory> _factories = new();
        // Key - interface type, value - implementation type;
        private readonly Dictionary<Type, Type> _interfaceToImplementationMap = new();
        // Key - implementation type;
        private readonly Dictionary<Type, ImplementationBehaviour> _implementationBehaviourTypeMap = new();

        /// <summary>
        /// Binds <typeparam name="TInterface"></typeparam> to it's default implementation, defined via <see cref="DefaultImplementationAttribute"/>.
        /// NOTE: <typeparam name="TInterface"></typeparam> hast to be decorated with <see cref="DefaultImplementationAttribute"/>.
        /// </summary>
        public void BindToDefaultImplementation<TInterface>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TInterface : class =>
            BindToDefaultImplementation(typeof(TInterface), behaviour);

        /// <summary>
        /// Binds <param name="interfaceType"></param> to it's default implementation, defined via <see cref="DefaultImplementationAttribute"/>.
        /// NOTE: <param name="interfaceType"></param> hast to be decorated with <see cref="DefaultImplementationAttribute"/>.
        /// </summary>
        public void BindToDefaultImplementation(Type interfaceType, ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
        {
            if (!interfaceType.IsInterface)
                throw new Exception($"The type '{interfaceType.FullName}' has to be an interface.");

            if (Attribute.GetCustomAttribute(interfaceType, typeof(DefaultImplementationAttribute)) is not DefaultImplementationAttribute defaultImplementationAttribute)
                throw new InvalidOperationException($"Unable to bind interface '{interfaceType.FullName}' to default implementation.");

            if (defaultImplementationAttribute.Implementation == null)
                throw new InvalidOperationException($"Unable to bind interface '{interfaceType.FullName}' to default implementation: implementation type is not declared.");

            var factoryType = typeof(DefaultFactory);
            if (defaultImplementationAttribute.FactoryType != null)
                factoryType = defaultImplementationAttribute.FactoryType;

            BindInternal(interfaceType, defaultImplementationAttribute.Implementation, factoryType, behaviour);
        }

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to itself.
        /// </summary>
        public void BindToSelf<TImplementation>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TImplementation : class =>
            BindToSelf<TImplementation, DefaultFactory>(behaviour);

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to itself using specific factory <typeparam name="TFactory"></typeparam>.
        /// </summary>
        public void BindToSelf<TImplementation, TFactory>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TImplementation : class
            where TFactory : InstanceFactory =>
            Bind<TImplementation, TImplementation, TFactory>(behaviour);

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to all the interfaces it implements.
        /// </summary>
        public void BindToAllImplementedInterfaces<TImplementation>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TImplementation : class =>
            BindToAllImplementedInterfaces<TImplementation, DefaultFactory>(behaviour);

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to all the interfaces it implements using specific factory <typeparam name="TFactory"></typeparam>.
        /// </summary>
        public void BindToAllImplementedInterfaces<TImplementation, TFactory>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TImplementation : class
            where TFactory : InstanceFactory
        {
            var implementationType = typeof(TImplementation);
            foreach (var interfaceType in implementationType.GetInterfaces())
                BindInternal(interfaceType, implementationType, typeof(TFactory), behaviour);
        }

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to <typeparam name="TInterface"></typeparam>.
        /// </summary>
        public void Bind<TInterface, TImplementation>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface =>
            Bind<TInterface, TImplementation, DefaultFactory>(behaviour);

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to <typeparam name="TInterface"></typeparam> using specific factory <typeparam name="TFactory"></typeparam>.
        /// </summary>
        public void Bind<TInterface, TImplementation, TFactory>(ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface
            where TFactory : InstanceFactory =>
            BindInternal(typeof(TInterface), typeof(TImplementation), typeof(TFactory), behaviour);

        /// <summary>
        /// Binds <typeparam name="TImplementation"></typeparam> to <typeparam name="TInterface"></typeparam>.
        /// Allows to delegate <param name="instanceProvider"></param> as a factory.
        /// </summary>
        public void Bind<TInterface, TImplementation>(Func<TImplementation> instanceProvider, ImplementationBehaviour behaviour = ImplementationBehaviour.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface =>
            BindInternal(typeof(TInterface), typeof(TImplementation), factoryInstanceBuilder: () =>
                CreateFactoryInstance(typeof(ProxyFactory<TImplementation>), instanceProvider), behaviour);

        /// <summary>
        /// Low level explicit binding function.
        /// Should only be used if its required to bind using raw types, for instance, via reflection - for any other means, use generic (type - constrained) functions instead.
        /// </summary>
        public void Bind(Type interfaceType, Type implementationType, Type factoryType, ImplementationBehaviour behaviour)
        {
            if (!interfaceType.IsAssignableFrom(implementationType))
                throw new InvalidOperationException($"Type '{implementationType!.FullName}' has to implement '{interfaceType.FullName}'.");

            if (!typeof(InstanceFactory).IsAssignableFrom(factoryType))
                throw new InvalidOperationException($"Type '{factoryType!.FullName}' has to inherit '{interfaceType.FullName}'.");

            BindInternal(interfaceType, implementationType, factoryType, behaviour);
        }

        #region PRIVATE
        private void BindInternal(Type interfaceType, Type implementationType, Type factoryType, ImplementationBehaviour behaviour) =>
            BindInternal(interfaceType, implementationType, factoryInstanceBuilder: () => CreateFactoryInstance(factoryType, implementationType), behaviour);

        private void BindInternal(Type interfaceType, Type implementationType, Func<InstanceFactory> factoryInstanceBuilder, ImplementationBehaviour behaviour)
        {
            if (!_interfaceToImplementationMap.TryAdd(interfaceType, implementationType))
                throw new InvalidOperationException($"Interface '{interfaceType.FullName}' already registered for type '{implementationType.FullName}'.");

            if (!_implementationBehaviourTypeMap.TryAdd(implementationType, behaviour))
            {
                // TODO: Decide if the following has to be an intended behaviour, or it should not be allowed.
                // Diagnostics - implementation behaviour is already registered.
                // Could potentially happen in following scenarios:
                // 1. When calling 'BindToAllImplementedInterfaces'.
                // 2. When the same implementation is bound to different interfaces using 'Bind', with different behaviour provided as an argument.
                var currentBehaviour = _implementationBehaviourTypeMap[implementationType];
                if (currentBehaviour != behaviour)
                    throw new InvalidOperationException($"Implementation '{implementationType.FullName}' is already bound to '{currentBehaviour}' behaviour, but new behaviour '{behaviour}' was requested.");
            }

            if (_factories.ContainsKey(implementationType))
                return;

            if (implementationType.IsInterface || implementationType.IsAbstract | implementationType.IsGenericType)
                throw new InvalidOperationException($"Implementation of type '{implementationType.FullName}' couldn't be interface, static, abstract, or generic.");

            if (!interfaceType.IsAssignableFrom(implementationType))
                throw new InvalidOperationException($"Implementation of type '{implementationType.FullName}' should be inherited for '{interfaceType.FullName}'.");

            _factories.Add(implementationType, factoryInstanceBuilder());
        }

        private static InstanceFactory CreateFactoryInstance(Type factoryType, params object[] args)
        {
            var factoryBaseType = typeof(InstanceFactory);
            if (!factoryBaseType.IsAssignableFrom(factoryType))
                throw new InvalidOperationException($"Factory of type '{factoryType!.FullName}' should be inherited for '{factoryBaseType.FullName}'.");

            if (Activator.CreateInstance(factoryType, args) is not InstanceFactory factory)
                throw new InvalidOperationException($"Unable to build factory for of type '{factoryType.FullName}'.");

            return factory;
        }
        #endregion

        /// <summary>
        /// We want to copy data here for safety reason.
        /// This allows to perform the following:
        /// 1. Create binderA.
        /// 2. Create scopeA with binderA.
        /// 3. Modify the binderA
        /// 2. Create scopeB with binderA.
        /// Changes that has been done in '3' won't affect scopeA.
        /// </summary>
        public ScopeDependencyMap ToDependencyMap() =>
            new(factories: new Dictionary<Type, InstanceFactory>(_factories),
                interfaceToImplementationMap: new Dictionary<Type, Type>(_interfaceToImplementationMap),
                implementationBehaviourTypeMap: new Dictionary<Type, ImplementationBehaviour>(_implementationBehaviourTypeMap));
    }
}