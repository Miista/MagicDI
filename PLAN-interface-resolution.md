# Plan: Interface Resolution for MagicDI

## Overview

Add automatic interface resolution to MagicDI. When resolving a type that is an interface (or abstract class), automatically find and resolve a concrete implementation.

## Behavior

When `Resolve<IFoo>()` is called:
1. Detect that `IFoo` is an interface
2. Automatically find concrete implementations of `IFoo`
3. If exactly **one** implementation → resolve it
4. If **zero** implementations → throw exception
5. If **multiple** implementations → throw exception

## Key Design Decisions

### Context-Aware Resolution ("Closest First")

Interface resolution is contextual - the "closest" implementation wins based on the requesting type's location.

**Search strategy:**
1. Search `requestingType`'s assembly first
2. Expand to referenced assemblies
3. Expand to all loaded assemblies
4. Stop as soon as exactly one candidate is found at a given level

**Example:**
```
AssemblyA:
  - ILogger (interface)
  - ConsoleLogger : ILogger
  - ServiceA(ILogger) → gets ConsoleLogger (same assembly)

AssemblyB:
  - FileLogger : ILogger
  - ServiceB(ILogger) → gets FileLogger (closest to ServiceB)
```

### No Global Caching for Interface Mappings

Since different requesters might resolve the same interface to different implementations, we don't cache interface → implementation mappings globally.

### Top-Level Resolution Context

For top-level `Resolve<IFoo>()` calls, we use `StackTrace` to find the calling type and use that as the search context.

### Separation of Concerns

- **`MagicDI.Resolve()`** - figures out WHAT concrete type to create (including interface → implementation)
- **`InstanceFactory`** - figures out HOW to create an instance (only deals with concrete types)
- **`LifetimeResolver`** - figures out the LIFETIME

## Implementation Details

### New File: `ImplementationFinder.cs`

```csharp
internal static class ImplementationFinder
{
    /// <summary>
    /// Returns a concrete type for the given type.
    /// If the type is already concrete, returns it as-is.
    /// If the type is an interface/abstract, finds and returns a concrete implementation.
    /// </summary>
    public static Type GetConcreteType(Type type, Type? requestingType)
    {
        // Already concrete? Return as-is
        if (type.IsClass && !type.IsAbstract)
            return type;

        // Interface or abstract - find implementation
        // 1. Search requestingType's assembly
        // 2. Search referenced assemblies
        // 3. Search all loaded assemblies
        // Throw if zero or multiple candidates
    }
}
```

### Modify: `MagicDI.cs`

Split Resolve into two methods:

```csharp
// Public API - uses stack walking for context
public T Resolve<T>()
{
    var callingType = GetCallingType();
    return (T)Resolve(typeof(T), callingType);
}

// Internal - explicit context, used for nested resolution
internal object Resolve(Type type, Type? requestingType)
{
    Type concreteType = ImplementationFinder.GetConcreteType(type, requestingType);

    // Cache check, lifetime resolution using concreteType...

    // Pass concreteType as context for its dependencies
    var factory = new InstanceFactory(
        paramType => Resolve(paramType, requestingType: concreteType)
    );
    return factory.CreateInstance(concreteType);
}

private static Type? GetCallingType()
{
    foreach (var frame in new StackTrace().GetFrames())
    {
        var declaringType = frame.GetMethod()?.DeclaringType;
        if (declaringType?.Assembly != typeof(MagicDI).Assembly)
            return declaringType;
    }
    return null;
}
```

### Modify: `LifetimeResolver.cs`

Add context parameter for interface resolution during lifetime analysis:

- `DetermineLifetime(Type type)` → `DetermineLifetime(Type type, Type? requestingType)`
- When analyzing constructor parameters, use `ImplementationFinder.GetConcreteType()` for interface parameters
- Pass context through recursive calls

### `InstanceFactory.cs` - No Changes

InstanceFactory only deals with concrete types. The resolver delegate it receives already handles interface resolution.

## File Changes Summary

| File | Change |
|------|--------|
| `ImplementationFinder.cs` | **New** - `GetConcreteType(type, requestingType)` |
| `MagicDI.cs` | Split into public `Resolve<T>()` + internal `Resolve(Type, Type?)`, add `GetCallingType()` |
| `LifetimeResolver.cs` | Add context parameter, use `ImplementationFinder` for interface parameters |
| `InstanceFactory.cs` | **No changes** |
| `MagicDITests.cs` | Add interface resolution tests |

## Implementation Steps

Each step should be committed separately before moving to the next.

- [x] **Step 1:** Create `ImplementationFinder.cs` with `GetConcreteType()` method
- [x] **Step 2:** Modify `MagicDI.cs` - split Resolve into public/internal methods, add `GetCallingType()`
- [x] **Step 3:** Modify `LifetimeResolver.cs` - add context parameter and use `ImplementationFinder`
- [x] **Step 4:** Add interface resolution tests to `MagicDITests.cs`

## Test Cases

- Interface with single implementation → resolves correctly
- Interface with no implementation → throws clear exception
- Interface with multiple implementations → throws clear exception
- Interface implementation with dependencies → dependencies also resolved
- Lifetime determined from implementation (e.g., implementation is `IDisposable` → Transient)
- Nested interface dependencies (class depends on interface which depends on another interface)
- Context-aware resolution (same interface resolves differently based on requester's assembly)
- Top-level interface resolution (uses calling type as context)
- Abstract class resolution (same behavior as interface)
