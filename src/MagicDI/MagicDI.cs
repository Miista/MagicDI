using System;
using System.Collections.Concurrent;
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
        /// Tracks the determined lifetime for all types.
        /// Computed recursively from type metadata without constructing instances.
        /// Uses ConcurrentDictionary for thread-safe access.
        /// </summary>
        private readonly ConcurrentDictionary<Type, Lifetime> _lifetimes = new();

        /// <summary>
        /// Tracks types currently being resolved on each thread to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the resolution stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _resolutionStack = new(() => []);

        /// <summary>
        /// Tracks types currently being analyzed for lifetime determination to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the lifetime analysis stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _lifetimeStack = new(() => []);

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
            var lifetime = DetermineLifetime(type);

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
        private static ConstructorInfo GetConstructor(Type type)
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
        /// Determines the lifetime for a type using the following priority:
        /// <list type="number">
        /// <item>Explicit [Lifetime] attribute override (with captive dependency validation)</item>
        /// <item>IDisposable types → Transient</item>
        /// <item>Cascade from dependencies (least cacheable wins)</item>
        /// <item>No dependencies → Singleton</item>
        /// </list>
        /// This method works recursively on type metadata without constructing instances.
        /// </summary>
        /// <param name="type">The type to determine lifetime for.</param>
        /// <returns>The inferred or explicitly specified lifetime.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a type is explicitly marked as Singleton but depends on a Transient type (captive dependency).
        /// </exception>
        private Lifetime DetermineLifetime(Type type)
        {
            // Already determined?
            if (_lifetimes.TryGetValue(type, out var cached))
                return cached;

            // Check for circular dependency in lifetime analysis
            if (!_lifetimeStack.Value.Add(type))
            {
                var chain = string.Join(" -> ", _lifetimeStack.Value.Select(t => t.Name)) + " -> " + type.Name;
                throw new InvalidOperationException(
                    $"Circular dependency detected while resolving {type.Name}. Resolution chain: {chain}");
            }

            try
            {
                var attr = type.GetCustomAttribute<LifetimeAttribute>();

                // Recursively determine dependency lifetimes
                var constructor = GetConstructor(type);
                var transientDependency = constructor.GetParameters()
                    .Select(p => p.ParameterType)
                    .FirstOrDefault(depType => DetermineLifetime(depType) == Lifetime.Transient);

                Lifetime lifetime;

                // 1. Check for explicit attribute override
                if (attr != null)
                {
                    // Validate: explicit Singleton with transient dependency is a captive dependency error
                    if (attr.Lifetime == Lifetime.Singleton && transientDependency != null)
                    {
                        throw new InvalidOperationException(
                            $"Captive dependency detected: Singleton '{type.Name}' depends on Transient '{transientDependency.Name}'. " +
                            $"This would cause the transient instance to be captured and never released. " +
                            $"Either remove the [Lifetime(Singleton)] attribute from '{type.Name}', or mark '{transientDependency.Name}' as Singleton.");
                    }

                    lifetime = attr.Lifetime;
                }
                // 2. IDisposable → Transient
                else if (typeof(IDisposable).IsAssignableFrom(type))
                {
                    lifetime = Lifetime.Transient;
                }
                // 3. Cascade from dependencies - use the least cacheable lifetime
                else if (transientDependency != null)
                {
                    lifetime = Lifetime.Transient;
                }
                // 4. No deps or all deps Singleton → Singleton
                else
                {
                    lifetime = Lifetime.Singleton;
                }

                _lifetimes[type] = lifetime;
                return lifetime;
            }
            finally
            {
                _lifetimeStack.Value.Remove(type);
            }
        }
    }
}
