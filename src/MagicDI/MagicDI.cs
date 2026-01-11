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

        /// <summary>
        /// Resolves an instance of the specified type by checking the cache first,
        /// then creating a new instance if not found.
        /// </summary>
        /// <param name="type">The type to resolve.</param>
        /// <returns>An instance of the specified type.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown lifetime is encountered.</exception>
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

        /// <summary>
        /// Creates a new instance of the specified type by finding and invoking the appropriate constructor.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>A new instance of the specified type.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type is a primitive type or when constructor invocation returns null.
        /// </exception>
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

        /// <summary>
        /// Gets the most appropriate constructor for the specified type.
        /// Selects the constructor with the most parameters, using metadata token order as a tiebreaker.
        /// </summary>
        /// <param name="type">The type to get the constructor for.</param>
        /// <returns>The selected constructor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type has no public constructors.</exception>
        private ConstructorInfo GetConstructor(Type type)
        {
            var appropriateConstructor = type.GetConstructors()
                .OrderByDescending(info => info.GetParameters().Length)
                .ThenBy(info => info.MetadataToken)
                .FirstOrDefault();

            if (appropriateConstructor == null)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it has no public constructors");

            return appropriateConstructor;
        }

        /// <summary>
        /// Resolves all constructor parameters by recursively resolving each parameter type.
        /// </summary>
        /// <param name="constructorInfo">The constructor whose parameters should be resolved.</param>
        /// <returns>An array of resolved parameter instances.</returns>
        private object[] ResolveConstructorArguments(ConstructorInfo constructorInfo)
        {
            return constructorInfo
                .GetParameters()
                .Select(info => info.ParameterType)
                .Select(Resolve)
                .ToArray();
        }

        /// <summary>
        /// Determines the lifetime for a resolved instance.
        /// Currently always returns <see cref="Lifetime.Singleton"/>.
        /// </summary>
        /// <param name="instance">The resolved instance (currently unused).</param>
        /// <returns>The lifetime to use for caching the instance.</returns>
        private Lifetime DetermineLifeTime(object instance)
        {
            return Lifetime.Singleton;
        }
    }
}
