using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MagicDI
{
    /// <summary>
    /// A lightweight dependency injection container that uses reflection
    /// to automatically resolve constructor dependencies.
    /// </summary>
    public class MagicDI
    {
        private readonly Dictionary<Type, InstanceRegistry> _registeredInstances = new Dictionary<Type, InstanceRegistry>();

        /// <summary>
        /// Resolves an instance of the specified type, automatically
        /// resolving all constructor dependencies.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>An instance of the specified type.</returns>
        public T Resolve<T>()
        {
            var resolved = Resolve(typeof(T));

            if (resolved is T result)
                return result;

            throw new InvalidOperationException(
                $"Failed to cast resolved instance of type {resolved?.GetType().Name ?? "null"} to requested type {typeof(T).Name}");
        }

        private object Resolve(Type type)
        {
            // 0. Check if we have already resolved the instance
            if (_registeredInstances.TryGetValue(type, out var instanceRegistry))
            {
                switch (instanceRegistry.Lifetime)
                {
                    case Lifetime.Scoped:
                        return ResolveInstance(type);
                    case Lifetime.Transient:
                        return ResolveInstance(type);
                    case Lifetime.Singleton:
                        return instanceRegistry.Value;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var resolvedInstance = ResolveInstance(type);

            // 4. Determine life time (Scoped, Transient, Singleton)
            var lifetime = DetermineLifeTime(resolvedInstance);

            // 5. Register created instance
            var registry = new InstanceRegistry { Type = type, Lifetime = lifetime, Value = resolvedInstance };
            _registeredInstances.Add(type, registry);

            // 6. Return created instance
            return registry.Value;
        }

        private object ResolveInstance(Type type)
        {
            if (type.IsPrimitive)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it is a primitive type");

            // 1. Find the most appropriate constructor
            ConstructorInfo constructorInfo = GetConstructor(type);

            // 2. Resolve arguments to said constructor
            object[] resolvedConstructorArguments = ResolveConstructorArguments(constructorInfo);

            // 3. Invoke constructor
            var instance = constructorInfo.Invoke(resolvedConstructorArguments);

            if (instance == null)
                throw new InvalidOperationException(
                    $"Constructor invocation for type {type.Name} returned null");

            return instance;
        }

        private ConstructorInfo GetConstructor(Type type)
        {
            var appropriateConstructor = type.GetConstructors().OrderByDescending(info => info.GetParameters().Length).FirstOrDefault();

            if (appropriateConstructor == null)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it has no public constructors");

            return appropriateConstructor;
        }

        private object[] ResolveConstructorArguments(ConstructorInfo constructorInfo)
        {
            return constructorInfo
                .GetParameters()
                .Select(info => info.ParameterType)
                .Select(Resolve)
                .ToArray();
        }

        private Lifetime DetermineLifeTime(object instance)
        {
            return Lifetime.Singleton;
        }
    }
}
