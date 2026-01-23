# Generic Type Resolution Plan

This document outlines the implementation plan for supporting generic type resolution in MagicDI.

## Scope

**Supported**: Resolving closed generic interfaces to open generic implementations.
```csharp
di.Resolve<IRepository<User>>();  // ✓ Returns Repository<User>
```

**Not supported**: Resolving open generics directly.
```csharp
di.Resolve<IRepository<>>();  // ✗ Not supported (also a compile error)
```

This is a resolution feature, not a registration feature. MagicDI finds open generic implementations at resolution time and closes them with the requested type arguments.

## Problem Statement

Currently, when resolving `IRepository<User>`, MagicDI scans assemblies using `IsAssignableFrom`:

```csharp
if (interfaceType.IsAssignableFrom(type))  // IRepository<User>.IsAssignableFrom(Repository<>)
```

This fails because `Repository<>` (open generic) doesn't implement `IRepository<User>` (closed generic). Only `Repository<User>` does.

## Solution

When the requested type is a closed generic (e.g., `IRepository<User>`), and we find an open generic type (e.g., `Repository<T>`), we should:

1. Check if the open generic implements the open generic interface
2. Close the generic with the requested type arguments
3. Verify the closed type implements the closed interface

```csharp
// Request: IRepository<User>
// Found: Repository<T> implements IRepository<T>
// Construct: Repository<User>
// Verify: Repository<User> implements IRepository<User> ✓
```

## Implementation

### Key Reflection APIs

| API | Purpose |
|-----|---------|
| `type.IsGenericType` | True for both `List<>` and `List<int>` |
| `type.IsGenericTypeDefinition` | True only for open generics like `List<>` |
| `type.GetGenericTypeDefinition()` | Gets `List<>` from `List<int>` |
| `type.GetGenericArguments()` | Gets `[int]` from `List<int>` |
| `type.MakeGenericType(args)` | Creates `List<int>` from `List<>` + `[int]` |
| `type.GetInterfaces()` | Gets all interfaces a type implements |

### Modified Resolution Flow

```
Request: IRepository<User>
           │
           ▼
    Is it a closed generic?
    (IsGenericType && !IsGenericTypeDefinition)
           │
           ├─► No: Use existing logic
           │
           └─► Yes: Extract info
                    │
                    ├─ Open interface: IRepository<>
                    └─ Type args: [User]
                           │
                           ▼
                    Scan for candidates
                           │
                           ▼
               ┌───────────────────────┐
               │ For each type:        │
               │                       │
               │ 1. Already closed?    │──► Check IsAssignableFrom directly
               │    (Repository<User>) │
               │                       │
               │ 2. Open generic?      │──► Check if implements open interface
               │    (Repository<T>)    │    then close and verify
               │                       │
               │ 3. Non-generic?       │──► Skip (can't implement generic interface)
               └───────────────────────┘
                           │
                           ▼
                    Return closed type
                    (Repository<User>)
```

### Code Changes

#### ImplementationFinder.cs

Replace `FindCandidatesInAssembly` with generic-aware version:

```csharp
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
        types = ex.Types.Where(t => t != null).ToArray()!;
    }
    catch
    {
        return candidates;
    }

    // Check if we're looking for a closed generic interface
    var isClosedGenericInterface = interfaceType.IsGenericType
        && !interfaceType.IsGenericTypeDefinition;

    Type? openInterfaceType = null;
    Type[]? requestedTypeArgs = null;

    if (isClosedGenericInterface)
    {
        openInterfaceType = interfaceType.GetGenericTypeDefinition();
        requestedTypeArgs = interfaceType.GetGenericArguments();
    }

    foreach (var type in types)
    {
        if (!type.IsClass || type.IsAbstract)
            continue;

        // Case 1: Direct match (non-generic or already-closed generic)
        if (interfaceType.IsAssignableFrom(type))
        {
            candidates.Add(type);
            continue;
        }

        // Case 2: Open generic that could be closed to match
        if (isClosedGenericInterface && type.IsGenericTypeDefinition)
        {
            var closedType = TryCloseGenericType(type, openInterfaceType!, requestedTypeArgs!);
            if (closedType != null && interfaceType.IsAssignableFrom(closedType))
            {
                candidates.Add(closedType);
            }
        }
    }

    return candidates;
}
```

#### New Helper Method

```csharp
/// <summary>
/// Attempts to close an open generic type to implement a specific closed generic interface.
/// Returns null if the type cannot be closed to implement the interface.
/// </summary>
private static Type? TryCloseGenericType(
    Type openType,           // Repository<>
    Type openInterfaceType,  // IRepository<>
    Type[] typeArguments)    // [User]
{
    // Check if the open type has the same number of type parameters
    var typeParams = openType.GetGenericArguments();
    if (typeParams.Length != typeArguments.Length)
    {
        // Different arity - might still work if the type implements
        // the interface with specific type mappings, but this is complex.
        // For now, only support simple 1:1 mapping.
        return null;
    }

    // Check if the open type implements the open interface
    if (!ImplementsOpenGenericInterface(openType, openInterfaceType))
    {
        return null;
    }

    // Try to close the type with the requested type arguments
    try
    {
        var closedType = openType.MakeGenericType(typeArguments);
        return closedType;
    }
    catch (ArgumentException)
    {
        // Type constraints not satisfied
        // e.g., Repository<T> where T : class, but User is a struct
        return null;
    }
}

/// <summary>
/// Checks if an open generic type implements an open generic interface.
/// e.g., Does Repository&lt;T&gt; implement IRepository&lt;T&gt;?
/// </summary>
private static bool ImplementsOpenGenericInterface(Type openType, Type openInterfaceType)
{
    // Get all interfaces the open type implements
    foreach (var iface in openType.GetInterfaces())
    {
        if (!iface.IsGenericType)
            continue;

        // Compare the generic type definitions
        if (iface.GetGenericTypeDefinition() == openInterfaceType)
        {
            return true;
        }
    }

    // Also check if it's a class inheriting from an open generic base
    var baseType = openType.BaseType;
    while (baseType != null)
    {
        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == openInterfaceType)
        {
            return true;
        }
        baseType = baseType.BaseType;
    }

    return false;
}
```

### Edge Cases

#### 1. Multiple Type Parameters

```csharp
public interface IMapper<TSource, TDest> { }
public class Mapper<TSource, TDest> : IMapper<TSource, TDest> { }

// Request: IMapper<User, UserDto>
// Found: Mapper<TSource, TDest>
// Close with: [User, UserDto]
// Result: Mapper<User, UserDto>
```

This works with the same logic - just more type arguments.

#### 2. Partial Closure

```csharp
public interface IRepository<T> { }
public class StringRepository<T> : IRepository<T> { }  // Still open
public class UserStringRepository : StringRepository<User> { }  // Closed

// Request: IRepository<User>
// UserStringRepository is found directly (already closed)
```

The existing `IsAssignableFrom` check handles this.

#### 3. Type Constraints

```csharp
public class Repository<T> : IRepository<T> where T : class { }

// Request: IRepository<int>
// Repository<int> would violate the constraint
// MakeGenericType throws ArgumentException
// We catch it and return null - no candidate found
```

#### 4. Complex Type Argument Mapping

```csharp
public interface IHandler<TRequest, TResponse> { }
public class Handler<T> : IHandler<T, Result<T>> { }  // TResponse is derived from T

// Request: IHandler<GetUser, Result<GetUser>>
// This is complex because Handler<T> maps T to both TRequest and TResponse
```

This is a complex case. The simple implementation assumes 1:1 type parameter mapping. For v1, we can document this as unsupported. Supporting it would require analyzing how the type parameters flow through the inheritance chain.

**Recommendation for v1**: Only support cases where:
- The open type has the same number of type parameters as the open interface
- The type parameters are used in the same order

#### 5. Nested Generics

```csharp
public interface IHandler<T> { }
public class ListHandler<T> : IHandler<List<T>> { }

// Request: IHandler<List<User>>
// ListHandler<T> implements IHandler<List<T>>
// Need to extract T=User from List<User>
```

This requires more sophisticated type argument inference. For v1, recommend documenting as unsupported.

#### 6. Covariance/Contravariance

```csharp
public interface IProducer<out T> { }
public class AnimalProducer : IProducer<Animal> { }

// Request: IProducer<Dog> where Dog : Animal
// AnimalProducer doesn't match (produces Animal, not Dog)
// But IProducer<Animal> is assignable to IProducer<Dog> due to covariance
```

`IsAssignableFrom` already handles this correctly for closed types.

### Caching Considerations

The `ImplementationFinder` currently doesn't cache results. With generic resolution, caching becomes more valuable because:

1. `MakeGenericType` has overhead
2. The same closed generic might be requested multiple times

**Option 1**: Cache at the `ImplementationFinder` level

```csharp
private static readonly ConcurrentDictionary<Type, Type> _implementationCache = new();

public static Type GetConcreteType(Type type, Type? requestingType)
{
    // For generics, cache key should include requesting context
    var cacheKey = (type, requestingType);
    return _cache.GetOrAdd(cacheKey, _ => FindImplementation(type, requestingType));
}
```

**Option 2**: Cache at the `MagicDI` level (already done for singletons)

The existing singleton cache in `MagicDI` already caches by concrete type, so repeated resolutions of `IRepository<User>` will hit the cache after the first resolution.

**Recommendation**: Start without additional caching in `ImplementationFinder`. The singleton/scoped caching in `MagicDI` should be sufficient for most cases. Add `ImplementationFinder` caching later if profiling shows it's needed.

### Testing Strategy

#### Basic Generic Resolution

```csharp
public class GenericResolutionTests
{
    [Fact]
    public void Resolves_closed_generic_interface_to_open_generic_implementation()
    {
        var di = new MagicDI();

        var repo = di.Resolve<IRepository<User>>();

        repo.Should().BeOfType<Repository<User>>();
    }

    [Fact]
    public void Resolves_generic_with_multiple_type_parameters()
    {
        var di = new MagicDI();

        var mapper = di.Resolve<IMapper<User, UserDto>>();

        mapper.Should().BeOfType<Mapper<User, UserDto>>();
    }

    [Fact]
    public void Injects_closed_generic_as_dependency()
    {
        var di = new MagicDI();

        var service = di.Resolve<UserService>();

        service.Repository.Should().BeOfType<Repository<User>>();
    }

    [Fact]
    public void Respects_type_constraints()
    {
        var di = new MagicDI();

        // Repository<T> where T : class
        // int is not a class, so this should fail
        var act = () => di.Resolve<IRepository<int>>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No implementation found*");
    }

    [Fact]
    public void Prefers_explicit_closed_type_over_open_generic()
    {
        var di = new MagicDI();

        // Both UserRepository and Repository<User> exist
        // UserRepository should be preferred (more specific)
        var repo = di.Resolve<IRepository<User>>();

        repo.Should().BeOfType<UserRepository>();
    }

    // Test types
    public interface IRepository<T> { }
    public class Repository<T> : IRepository<T> { }
    public class UserRepository : IRepository<User> { }
    public class User { }

    public interface IMapper<TSource, TDest> { }
    public class Mapper<TSource, TDest> : IMapper<TSource, TDest> { }
    public class UserDto { }

    public class UserService(IRepository<User> repository)
    {
        public IRepository<User> Repository { get; } = repository;
    }
}
```

#### Namespace Proximity with Generics

```csharp
[Fact]
public void Applies_namespace_proximity_to_generic_implementations()
{
    // If multiple open generics implement IRepository<>,
    // the one closest to the requesting type should be chosen
}
```

#### Lifetime with Generics

```csharp
[Fact]
public void Generic_type_respects_lifetime_attribute()
{
    var di = new MagicDI();

    var repo1 = di.Resolve<IRepository<User>>();
    var repo2 = di.Resolve<IRepository<User>>();

    // Repository<T> has [Lifetime(Lifetime.Transient)]
    repo1.Should().NotBeSameAs(repo2);
}

[Fact]
public void Different_closed_generics_are_cached_separately()
{
    var di = new MagicDI();

    var userRepo = di.Resolve<IRepository<User>>();
    var orderRepo = di.Resolve<IRepository<Order>>();

    userRepo.Should().BeOfType<Repository<User>>();
    orderRepo.Should().BeOfType<Repository<Order>>();
}
```

### Documentation Updates

#### README Changes

Replace the "No Open Generic Resolution" limitation with documentation of the supported feature:

```markdown
### Generic Type Resolution

MagicDI automatically resolves generic interfaces to their open generic implementations:

\`\`\`csharp
public interface IRepository<T> { }
public class Repository<T> : IRepository<T> { }

var di = new MagicDI();
var userRepo = di.Resolve<IRepository<User>>();    // Returns Repository<User>
var orderRepo = di.Resolve<IRepository<Order>>();  // Returns Repository<Order>
\`\`\`

Each closed generic is treated as a separate type for caching:
- `Repository<User>` and `Repository<Order>` are different singletons

**Limitations**:
- Type parameters must map 1:1 (same count, same order)
- Complex type argument inference is not supported:
  \`\`\`csharp
  // NOT supported:
  public class Handler<T> : IHandler<T, Result<T>> { }
  \`\`\`
```

### File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `ImplementationFinder.cs` | Modified | Add generic type resolution logic |
| `MagicDITests.Generics.cs` | New | Generic resolution tests |
| `README.md` | Modified | Document generic support, update limitations |

### Implementation Checklist

- [ ] Add `TryCloseGenericType` helper method
- [ ] Add `ImplementsOpenGenericInterface` helper method
- [ ] Modify `FindCandidatesInAssembly` to handle generics
- [ ] Add tests for basic generic resolution
- [ ] Add tests for multiple type parameters
- [ ] Add tests for type constraints
- [ ] Add tests for namespace proximity with generics
- [ ] Add tests for lifetime with generics
- [ ] Update README documentation
- [ ] Remove "No Open Generic Resolution" from limitations

### Open Questions

1. **Priority of explicit vs open**: If both `UserRepository : IRepository<User>` and `Repository<T> : IRepository<T>` exist, which should win?

   **Recommendation**: Explicit closed type wins (more specific). Implementation: check non-generic candidates first, then fall back to closing open generics.

2. **What about abstract generic bases?**
   ```csharp
   public abstract class RepositoryBase<T> : IRepository<T> { }
   public class Repository<T> : RepositoryBase<T> { }
   ```

   **Answer**: Works automatically. `Repository<T>` is concrete and implements `IRepository<T>` through inheritance.

3. **Performance impact of scanning for generics?**

   The additional reflection calls (`GetGenericTypeDefinition`, `MakeGenericType`) add overhead. However:
   - Only triggered for generic interface requests
   - Results are cached via singleton/scoped caching
   - First resolution is typically during startup

   **Recommendation**: Acceptable for v1. Profile if issues arise.
