using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MagicDI
{
    /// <summary>
    /// A lightweight dependency injection container that uses reflection
    /// to automatically resolve constructor dependencies.
    /// </summary>
    public class MagicDI
    {
        private readonly object _singletonLock = new();
        private readonly Dictionary<Type, object> _singletons = new();

        /// <summary>
        /// Resolves lifetime for types based on metadata analysis.
        /// </summary>
        private readonly LifetimeResolver _lifetimeResolver = new();

        /// <summary>
        /// Tracks types currently being resolved on each thread to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the resolution stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _resolutionStack = new(() => []);

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
        private object Resolve(Type type)
        {
            // Check if we have a cached singleton instance (fast path, no lock)
            if (_singletons.TryGetValue(type, out var cached))
            {
                return cached;
            }

            // Determine lifetime from type metadata (recursive, no instance needed)
            var lifetime = _lifetimeResolver.DetermineLifetime(type);

            if (lifetime == Lifetime.Singleton)
            {
                lock (_singletonLock)
                {
                    // Double-check after acquiring lock
                    if (_singletons.TryGetValue(type, out cached))
                    {
                        return cached;
                    }

                    var instance = ResolveInstance(type);
                    _singletons.Add(type, instance);
                    return instance;
                }
            }

            // Transient - no locking needed, create new instance each time
            return ResolveInstance(type);
        }

        /// <summary>
        /// Creates a new instance of the specified type by finding and invoking the appropriate constructor.
        /// </summary>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>A new instance of the specified type.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type is a primitive type, when a circular dependency is detected,
        /// or when constructor invocation returns null.
        /// </exception>
        private object ResolveInstance(Type type)
        {
            if (type.IsPrimitive)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it is a primitive type");

            // Check for circular dependency
            if (!_resolutionStack.Value.Add(type))
            {
                var chain = string.Join(" -> ", _resolutionStack.Value.Select(t => t.Name)) + " -> " + type.Name;
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving {type.Name}. Resolution chain: {chain}");
            }

            try
            {
                // 1. Find the most appropriate constructor
                var constructorInfo = GetConstructor(type);

                // 2. Resolve arguments to said constructor
                var resolvedConstructorArguments = ResolveConstructorArguments(constructorInfo);

                // 3. Invoke constructor
                var instance = constructorInfo.Invoke(resolvedConstructorArguments);

                if (instance == null)
                    throw new InvalidOperationException(
                        $"Constructor invocation for type {type.Name} returned null");

                return instance;
            }
            finally
            {
                // Always remove the type from the resolution stack when done
                _resolutionStack.Value.Remove(type);
            }
        }

        /// <summary>
        /// Gets the most appropriate constructor for the specified type.
        /// Selects the constructor with the most parameters, using metadata token order as a tiebreaker.
        /// </summary>
        /// <param name="type">The type to get the constructor for.</param>
        /// <returns>The selected constructor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type has no public constructors.</exception>
        private static ConstructorInfo GetConstructor(Type type) => LifetimeResolver.GetConstructor(type);

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
    }
}
