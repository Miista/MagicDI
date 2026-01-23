using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MagicDI
{
    /// <summary>
    /// Finds concrete implementations for interfaces and abstract classes.
    /// Uses a "closest first" search strategy based on the requesting type's assembly.
    /// </summary>
    internal static class ImplementationFinder
    {
        /// <summary>
        /// Returns a concrete type for the given type.
        /// If the type is already concrete, returns it as-is.
        /// If the type is an interface or abstract class, finds and returns a concrete implementation.
        /// </summary>
        /// <param name="type">The type to resolve to a concrete type.</param>
        /// <param name="requestingType">The type requesting the resolution, used to determine search order.</param>
        /// <returns>A concrete type that can be instantiated.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no implementation is found or when multiple implementations are found.
        /// </exception>
        public static Type GetConcreteType(Type type, Type? requestingType)
        {
            // Already concrete? Return as-is
            if (type.IsClass && !type.IsAbstract)
            {
                return type;
            }

            // Interface or abstract - find implementation
            return FindImplementation(type, requestingType);
        }

        private static Type FindImplementation(Type interfaceType, Type? requestingType)
        {
            var assemblies = GetAssembliesInSearchOrder(requestingType);

            foreach (var assembly in assemblies)
            {
                var candidates = FindCandidatesInAssembly(interfaceType, assembly);

                if (candidates.Count == 0)
                {
                    // No candidates in this assembly, continue to next
                    continue;
                }

                if (candidates.Count == 1)
                {
                    return candidates[0];
                }

                // Multiple candidates - use namespace proximity to disambiguate
                var closest = FindClosestByNamespace(candidates, requestingType);
                if (closest != null)
                {
                    return closest;
                }

                // Multiple candidates at the same distance - ambiguous
                var candidateNames = string.Join(", ", candidates.Select(c => c.FullName));
                throw new InvalidOperationException(
                    $"Multiple implementations found for {interfaceType.FullName}: {candidateNames}. " +
                    "Cannot resolve ambiguous interface.");
            }

            throw new InvalidOperationException(
                $"No implementation found for {interfaceType.FullName}. " +
                "Ensure a concrete class implementing this interface exists.");
        }

        /// <summary>
        /// Finds the single closest candidate by namespace distance.
        /// Returns null if there are multiple candidates at the same minimum distance.
        /// </summary>
        private static Type? FindClosestByNamespace(List<Type> candidates, Type? requestingType)
        {
            if (requestingType == null)
            {
                return null; // No context to determine proximity
            }

            var withDistances = candidates
                .Select(c => (Type: c, Distance: GetNamespaceDistance(requestingType, c)))
                .OrderBy(x => x.Distance)
                .ToList();

            var minDistance = withDistances[0].Distance;
            var closestCandidates = withDistances.Where(x => x.Distance == minDistance).ToList();

            if (closestCandidates.Count == 1)
            {
                return closestCandidates[0].Type;
            }

            return null; // Multiple candidates at same distance
        }

        /// <summary>
        /// Calculates the namespace distance between two types.
        /// Distance 0 = same namespace
        /// Distance increases as namespaces diverge
        /// </summary>
        private static int GetNamespaceDistance(Type from, Type to)
        {
            var fromParts = (from.Namespace ?? "").Split('.');
            var toParts = (to.Namespace ?? "").Split('.');

            // Find common prefix length
            var commonLength = 0;
            var minLength = Math.Min(fromParts.Length, toParts.Length);
            for (var i = 0; i < minLength; i++)
            {
                if (fromParts[i] == toParts[i])
                {
                    commonLength++;
                }
                else
                {
                    break;
                }
            }

            // Distance = steps up from 'from' to common ancestor + steps down to 'to'
            var stepsUp = fromParts.Length - commonLength;
            var stepsDown = toParts.Length - commonLength;
            return stepsUp + stepsDown;
        }

        private static IEnumerable<Assembly> GetAssembliesInSearchOrder(Type? requestingType)
        {
            var visited = new HashSet<Assembly>();

            // 1. Start with requesting type's assembly (closest)
            if (requestingType != null)
            {
                var requestingAssembly = requestingType.Assembly;
                if (visited.Add(requestingAssembly))
                {
                    yield return requestingAssembly;
                }

                // 2. Search referenced assemblies
                foreach (var referencedName in requestingAssembly.GetReferencedAssemblies())
                {
                    Assembly? referencedAssembly = null;
                    try
                    {
                        referencedAssembly = Assembly.Load(referencedName);
                    }
                    catch
                    {
                        // Skip assemblies that can't be loaded
                    }

                    if (referencedAssembly != null && visited.Add(referencedAssembly))
                    {
                        yield return referencedAssembly;
                    }
                }
            }

            // 3. Search all loaded assemblies (furthest fallback)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (visited.Add(assembly))
                {
                    yield return assembly;
                }
            }
        }

        private static List<Type> FindCandidatesInAssembly(Type interfaceType, Assembly assembly)
        {
            var candidates = new List<Type>();

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types couldn't be loaded, use the ones that could
                types = ex.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                // Assembly can't be inspected, skip it
                return candidates;
            }

            foreach (var type in types)
            {
                if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
                {
                    candidates.Add(type);
                }
            }

            return candidates;
        }
    }
}
