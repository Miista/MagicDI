# Plan: IEnumerable<T> Interface Resolution

## Overview

Add support for resolving `IEnumerable<TInterface>` to return **all implementations** of `TInterface`. This enables scenarios where multiple implementations of the same interface coexist and should all be injected together.

### Example Usage

```csharp
public interface INotificationHandler { void Handle(string message); }

public class EmailHandler : INotificationHandler { ... }
public class SmsHandler : INotificationHandler { ... }
public class SlackHandler : INotificationHandler { ... }

public class NotificationService
{
    public NotificationService(IEnumerable<INotificationHandler> handlers)
    {
        // handlers contains: EmailHandler, SmsHandler, SlackHandler
        foreach (var handler in handlers)
            handler.Handle("Hello!");
    }
}
```

## Behavior Specification

### 1. Detection

When `Resolve<T>()` is called:
- Check if `T` is `IEnumerable<TElement>` (or `ICollection<T>`, `IList<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`)
- Extract `TElement` from the generic argument
- Proceed with enumerable resolution for any `TElement` type (interfaces find all implementations, concrete types return single-element list)

### 2. Implementation Discovery

Find ALL implementations of `TElement`:
- **Current behavior**: `ImplementationFinder` stops at first assembly with exactly one match
- **New behavior**: New method `FindAllImplementations()` collects from ALL searched assemblies
- Search order remains: requesting type's assembly → referenced assemblies → all loaded assemblies
- Include implementations even if they were previously instantiated as singletons

### 3. Instance Creation

For each discovered implementation:
- Resolve using the standard `Resolve()` path (respects individual lifetimes)
- Singletons are reused if already created
- Transients get new instances
- Each implementation's lifetime is determined independently

### 4. Return Type

- Return as `List<TElement>` (implements all common collection interfaces)
- The enumerable itself is always transient (new list each time)
- Individual elements follow their own lifetime rules

### 5. Edge Cases

| Scenario | Behavior |
|----------|----------|
| Zero implementations | Return empty `List<T>` (not an error) |
| One implementation | Return single-element list |
| Mixed lifetimes | Each element follows its own lifetime |
| Nested `IEnumerable` in dependency | Supported, resolved recursively |
| `IEnumerable<ConcreteType>` | Return single-element list with that type |
| `IEnumerable<int>` or primitives | Throw `InvalidOperationException` |

### 6. Context Awareness

- Use the same "closest first" assembly search as single-interface resolution
- Context (requesting type) determines search scope
- All matching implementations from all searched assemblies are included

## Implementation Steps

### Step 1: Add EnumerableResolver Helper Class

Create `src/MagicDI/EnumerableResolver.cs`:

```csharp
internal static class EnumerableResolver
{
    // Check if type is IEnumerable<T> or related collection interface
    public static bool IsEnumerableRequest(Type type, out Type? elementType)

    // Find all implementations and create instances
    public static object ResolveAll(Type elementType, Type? requestingType, Func<Type, Type?, object> resolver)
}
```

**Responsibilities:**
- Detect `IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`
- Delegate to `ImplementationFinder.FindAllImplementations()`
- Create list and populate via resolver delegate
- Handle empty results gracefully

### Step 2: Extend ImplementationFinder

Add to `src/MagicDI/ImplementationFinder.cs`:

```csharp
// Existing method - finds exactly one
public static Type GetConcreteType(Type type, Type? requestingType)

// New method - finds all
public static IReadOnlyList<Type> FindAllImplementations(Type interfaceType, Type? requestingType)
```

**Changes:**
- Extract common assembly search logic into shared helper
- `FindAllImplementations` collects from all searched assemblies (doesn't stop at first match)
- Returns empty list if no implementations found (not an error)
- Filter out abstract classes and interfaces from results

### Step 3: Modify MagicDI.Resolve

Update `src/MagicDI/MagicDI.cs`:

```csharp
// NOTE: This is an INSTANCE method (not static) because it accesses
// _singletons, _contextStack, _lifetimeResolver, and _instanceFactory
private object Resolve(Type type, Type? requestingType)
{
    // NEW: Check for IEnumerable<T> request first
    if (EnumerableResolver.IsEnumerableRequest(type, out var elementType))
    {
        return EnumerableResolver.ResolveAll(elementType!, requestingType, Resolve);
    }

    // Existing resolution logic...
}
```

### Step 4: Handle Lifetime Edge Cases

In `LifetimeResolver.cs`:
- `IEnumerable<T>` requests don't need lifetime caching (always create new list)
- Individual elements use their normal lifetime resolution
- No changes needed if EnumerableResolver calls standard Resolve for each element

### Step 5: Comprehensive Tests

Create `src/MagicDI.Tests/MagicDITests.EnumerableResolution.cs`:

```csharp
public class EnumerableResolutionTests
{
    // Basic functionality
    [Fact] ResolveIEnumerable_WithMultipleImplementations_ReturnsAll()
    [Fact] ResolveIEnumerable_WithSingleImplementation_ReturnsSingleElementList()
    [Fact] ResolveIEnumerable_WithNoImplementations_ReturnsEmptyList()

    // Collection interface variations
    [Fact] ResolveICollection_ReturnsAllImplementations()
    [Fact] ResolveIList_ReturnsAllImplementations()
    [Fact] ResolveIReadOnlyList_ReturnsAllImplementations()
    [Fact] ResolveIReadOnlyCollection_ReturnsAllImplementations()

    // Lifetime behavior
    [Fact] ResolveIEnumerable_SingletonElements_ReusesSameInstances()
    [Fact] ResolveIEnumerable_TransientElements_CreatesNewInstances()
    [Fact] ResolveIEnumerable_MixedLifetimes_RespectsEachElementLifetime()
    [Fact] ResolveIEnumerable_CalledTwice_CreatesNewListButReusesSingletons()

    // Dependency injection scenarios
    [Fact] Constructor_WithIEnumerableDependency_ReceivesAllImplementations()
    [Fact] NestedDependencies_WithIEnumerable_ResolvesCorrectly()

    // Edge cases
    [Fact] ResolveIEnumerable_OfConcreteType_ReturnsSingleElementList()
    [Fact] ResolveIEnumerable_OfPrimitive_ThrowsInvalidOperationException()
    [Fact] ResolveIEnumerable_OfString_ThrowsInvalidOperationException()

    // Context awareness
    [Fact] ResolveIEnumerable_FromDifferentAssemblies_UsesContextAppropriately()

    // Thread safety
    [Fact] ResolveIEnumerable_ConcurrentCalls_ThreadSafe()
}
```

## Detailed Design Decisions

### Why Return Empty List Instead of Throwing?

Unlike single-interface resolution (which throws on zero implementations), `IEnumerable<T>` returns empty:
- Matches behavior of other DI containers (Microsoft.Extensions.DependencyInjection, Autofac)
- Valid use case: optional handlers, plugins, or extensions
- Consumer can check `Any()` if at least one is required

### Why Always Transient for the List?

The list container is always new because:
- Implementations may be transient (shouldn't cache references)
- Singleton sharing would be confusing (list contents could change)
- Matches standard DI container behavior
- Low cost (list creation is cheap)

### Assembly Search Scope

Use same search scope as context-aware single resolution:
- Ensures consistent behavior
- If requesting type is in Assembly A, finds implementations visible to A
- Prevents "leaking" implementations from unrelated assemblies

### Supported Collection Types

Support common read-side collection interfaces:
- `IEnumerable<T>` - most common
- `ICollection<T>` - adds Count
- `IList<T>` - adds indexing
- `IReadOnlyList<T>` - immutable contract
- `IReadOnlyCollection<T>` - immutable with Count

NOT supported (to avoid complexity):
- `T[]` arrays
- `List<T>` concrete type
- `HashSet<T>` and other specialized collections

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `EnumerableResolver.cs` | **New** | Detection and multi-resolution logic |
| `ImplementationFinder.cs` | Modify | Add `FindAllImplementations()` method |
| `MagicDI.cs` | Modify | Add enumerable check at start of Resolve |
| `MagicDITests.EnumerableResolution.cs` | **New** | Comprehensive test coverage |

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Performance with many implementations | Lazy enumeration not supported, but list creation is fast |
| Breaking existing behavior | Enumerable check is additive, existing paths unchanged |
| Circular dependencies with enumerable | Standard circular detection still applies per-element |
| Thread safety | List creation is atomic; elements use existing thread-safe resolution |

## Future Considerations

Not in scope for this implementation, but possible future enhancements:
- `Lazy<IEnumerable<T>>` for deferred resolution
- Filtering implementations by attribute or convention
- Ordering implementations by priority attribute
- `IServiceProvider` compatibility shim

---

## Review Notes (2026-01-22)

### Code Review Findings

1. **Feature Status**: NOT IMPLEMENTED - No `EnumerableResolver.cs` exists, no `FindAllImplementations()` in `ImplementationFinder.cs`

2. **Existing Infrastructure**: The following can be reused:
   - `ImplementationFinder.GetAssembliesInSearchOrder()` - assembly search order logic
   - `ImplementationFinder.FindCandidatesInAssembly()` - finds implementations in a single assembly
   - `MagicDI.Resolve(Type, Type?)` - can be called per-element for individual resolution

3. **Plan Corrections Applied**:
   - Step 3: Fixed method signature from `static` to instance method (it needs access to `_singletons`, `_contextStack`, etc.)
   - Step 1: Clarified that enumerable handling applies to all element types, not just interfaces/abstracts
