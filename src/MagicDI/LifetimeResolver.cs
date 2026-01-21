using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace MagicDI
{
    /// <summary>
    /// Determines the lifetime for types using metadata analysis.
    /// </summary>
    internal class LifetimeResolver
    {
        /// <summary>
        /// Tracks the determined lifetime for all types.
        /// Computed recursively from type metadata without constructing instances.
        /// Uses ConcurrentDictionary for thread-safe access.
        /// </summary>
        private readonly ConcurrentDictionary<Type, Lifetime> _lifetimes = new();

        /// <summary>
        /// Tracks types currently being analyzed for lifetime determination to detect circular dependencies.
        /// Using ThreadLocal ensures thread-safety for the lifetime analysis stack.
        /// </summary>
        private readonly ThreadLocal<HashSet<Type>> _lifetimeStack = new(() => []);

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
        public Lifetime DetermineLifetime(Type type)
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
                var constructor = ConstructorSelector.GetConstructor(type);
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
