# Scoped Lifetime Implementation Plan

This document outlines the implementation plan for adding `CreateScope()` and `Lifetime.Scoped` support to MagicDI.

## Overview

Scoped lifetime creates instances that are singletons within a scope but different across scopes. This is the standard pattern for web requests, database transactions, and unit-of-work scenarios.

```csharp
// Target API
using var scope = di.CreateScope();
var ctx1 = scope.Resolve<DbContext>(); // New instance
var ctx2 = scope.Resolve<DbContext>(); // Same instance as ctx1
// Disposing scope disposes ctx1/ctx2

using var scope2 = di.CreateScope();
var ctx3 = scope2.Resolve<DbContext>(); // Different instance from ctx1
```

## Design Goals

1. **Minimal API surface** - Single `CreateScope()` method
2. **Automatic disposal** - Scope disposes all IDisposables it created
3. **Thread safety** - Scopes should be usable across threads (but typically aren't)
4. **Backward compatible** - Existing code continues to work unchanged

## Architecture

### New Types

```
MagicDI (modified)
    ├── CreateScope() : IMagicScope
    └── Resolve<T>() (unchanged - resolves at root level)

IMagicScope : IDisposable
    ├── Resolve<T>() : T
    └── Dispose()

MagicScope (internal)
    ├── _parent : MagicDI
    ├── _scopedInstances : ConcurrentDictionary<Type, object>
    ├── _disposables : List<IDisposable>
    └── _disposed : bool
```

### Resolution Flow

```
scope.Resolve<T>()
    │
    ├─► Determine lifetime (existing LifetimeResolver)
    │
    ├─► Lifetime.Singleton
    │       └─► Delegate to parent MagicDI._singletons
    │
    ├─► Lifetime.Scoped
    │       └─► Check scope._scopedInstances
    │           ├─► Found: return cached
    │           └─► Not found: create, cache, track if IDisposable
    │
    └─► Lifetime.Transient
            └─► Create new instance, track if IDisposable
```

## Implementation Steps

### Phase 1: Core Infrastructure

#### 1.1 Add IMagicScope Interface

Create `src/MagicDI/IMagicScope.cs`:

```csharp
namespace MagicDI
{
    /// <summary>
    /// Represents a scope for resolving dependencies.
    /// Scoped instances are singletons within this scope.
    /// Disposing the scope disposes all IDisposable instances created within it.
    /// </summary>
    public interface IMagicScope : IDisposable
    {
        /// <summary>
        /// Resolves an instance within this scope.
        /// </summary>
        T Resolve<T>();
    }
}
```

#### 1.2 Implement MagicScope Class

Create `src/MagicDI/MagicScope.cs`:

```csharp
namespace MagicDI
{
    internal class MagicScope : IMagicScope
    {
        private readonly MagicDI _root;
        private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
        private readonly List<IDisposable> _disposables = new();
        private readonly object _disposeLock = new();
        private bool _disposed;

        internal MagicScope(MagicDI root)
        {
            _root = root;
        }

        public T Resolve<T>()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _root.ResolveInScope<T>(this);
        }

        internal object GetOrCreateScoped(Type type, Func<object> factory)
        {
            return _scopedInstances.GetOrAdd(type, _ =>
            {
                var instance = factory();
                TrackDisposable(instance);
                return instance;
            });
        }

        internal void TrackDisposable(object instance)
        {
            if (instance is IDisposable disposable)
            {
                lock (_disposeLock)
                {
                    if (!_disposed)
                    {
                        _disposables.Add(disposable);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            List<IDisposable> toDispose;
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
                toDispose = new List<IDisposable>(_disposables);
                _disposables.Clear();
            }

            // Dispose in reverse order (LIFO)
            for (int i = toDispose.Count - 1; i >= 0; i--)
            {
                try
                {
                    toDispose[i].Dispose();
                }
                catch
                {
                    // Swallow disposal exceptions to ensure all disposables are attempted
                    // Consider: aggregate exceptions or logging
                }
            }
        }
    }
}
```

#### 1.3 Modify MagicDI Class

Add to `src/MagicDI/MagicDI.cs`:

```csharp
public class MagicDI
{
    // ... existing fields ...

    /// <summary>
    /// Creates a new scope for resolving scoped dependencies.
    /// </summary>
    public IMagicScope CreateScope()
    {
        return new MagicScope(this);
    }

    /// <summary>
    /// Internal method for resolving within a scope context.
    /// </summary>
    internal T ResolveInScope<T>(MagicScope scope)
    {
        var callingType = GetCallingType();
        var resolved = Resolve(typeof(T), callingType, scope);

        if (resolved is T result)
            return result;

        throw new InvalidOperationException(
            $"Failed to cast resolved instance to {typeof(T).Name}");
    }

    // Modify existing Resolve to accept optional scope
    private object Resolve(Type type, Type? requestingType, MagicScope? scope = null)
    {
        var concreteType = ImplementationFinder.GetConcreteType(type, requestingType);
        var lifetime = _lifetimeResolver.DetermineLifetime(concreteType);

        // Singleton: always from root container
        if (lifetime == Lifetime.Singleton)
        {
            return GetOrCreateSingleton(concreteType);
        }

        // Scoped: from scope cache, or error if no scope
        if (lifetime == Lifetime.Scoped)
        {
            if (scope == null)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve scoped type '{concreteType.Name}' from root container. " +
                    "Use CreateScope() to create a scope first.");
            }

            return scope.GetOrCreateScoped(concreteType, () =>
            {
                _contextStack.Value.Push(concreteType);
                try
                {
                    return _instanceFactory.CreateInstance(concreteType, scope);
                }
                finally
                {
                    _contextStack.Value.Pop();
                }
            });
        }

        // Transient: always new, track in scope if available
        _contextStack.Value.Push(concreteType);
        try
        {
            var instance = _instanceFactory.CreateInstance(concreteType, scope);
            scope?.TrackDisposable(instance);
            return instance;
        }
        finally
        {
            _contextStack.Value.Pop();
        }
    }

    private object GetOrCreateSingleton(Type concreteType)
    {
        if (_singletons.TryGetValue(concreteType, out var cached))
            return cached;

        lock (_singletonLock)
        {
            if (_singletons.TryGetValue(concreteType, out cached))
                return cached;

            _contextStack.Value.Push(concreteType);
            try
            {
                var instance = _instanceFactory.CreateInstance(concreteType, scope: null);
                _singletons[concreteType] = instance;
                return instance;
            }
            finally
            {
                _contextStack.Value.Pop();
            }
        }
    }
}
```

### Phase 2: Lifetime Resolution Updates

#### 2.1 Update LifetimeResolver

The existing `LifetimeResolver` needs to handle `Lifetime.Scoped`:

```csharp
// In DetermineLifetime method, add handling for Scoped attribute
if (attr != null)
{
    // Validate captive dependencies for both Singleton and Scoped
    if ((attr.Lifetime == Lifetime.Singleton || attr.Lifetime == Lifetime.Scoped)
        && transientDependency != null)
    {
        throw new InvalidOperationException(
            $"Captive dependency detected: {attr.Lifetime} '{concreteType.Name}' " +
            $"depends on Transient '{transientDependency.Name}'.");
    }

    // Validate: Singleton depending on Scoped is also a captive dependency
    if (attr.Lifetime == Lifetime.Singleton && scopedDependency != null)
    {
        throw new InvalidOperationException(
            $"Captive dependency detected: Singleton '{concreteType.Name}' " +
            $"depends on Scoped '{scopedDependency.Name}'.");
    }

    lifetime = attr.Lifetime;
}
```

#### 2.2 Scoped Inference Rules

Unlike Singleton (default) and Transient (inferred from IDisposable), Scoped should **not** be auto-inferred. It must be explicitly declared:

```csharp
[Lifetime(Lifetime.Scoped)]
public class DbContext : IDisposable { }
```

Rationale: Scoped lifetime has specific semantics (per-request, per-transaction) that can't be reliably inferred from type metadata.

### Phase 3: InstanceFactory Updates

#### 3.1 Pass Scope Through Resolution

Modify `InstanceFactory` to accept scope context:

```csharp
internal class InstanceFactory
{
    private readonly Func<Type, MagicScope?, object> _resolver;

    public InstanceFactory(Func<Type, MagicScope?, object> resolver)
    {
        _resolver = resolver;
    }

    public object CreateInstance(Type type, MagicScope? scope)
    {
        // ... existing validation ...

        var constructorInfo = ConstructorSelector.GetConstructor(type);
        var args = constructorInfo
            .GetParameters()
            .Select(p => _resolver(p.ParameterType, scope))
            .ToArray();

        return constructorInfo.Invoke(args);
    }
}
```

### Phase 4: Captive Dependency Validation

#### 4.1 Lifetime Hierarchy

Lifetimes have a hierarchy for captive dependency validation:

```
Singleton (longest-lived)
    └── cannot depend on Scoped or Transient

Scoped (medium-lived)
    └── cannot depend on Transient

Transient (shortest-lived)
    └── can depend on anything
```

#### 4.2 Validation Matrix

| Parent Lifetime | Can Depend On |
|----------------|---------------|
| Singleton | Singleton only |
| Scoped | Singleton, Scoped |
| Transient | Singleton, Scoped, Transient |

#### 4.3 Implementation

```csharp
private void ValidateCaptiveDependency(
    Type parentType,
    Lifetime parentLifetime,
    Type dependencyType,
    Lifetime dependencyLifetime)
{
    bool isCaptive = parentLifetime switch
    {
        Lifetime.Singleton => dependencyLifetime != Lifetime.Singleton,
        Lifetime.Scoped => dependencyLifetime == Lifetime.Transient,
        Lifetime.Transient => false,
        _ => false
    };

    if (isCaptive)
    {
        throw new InvalidOperationException(
            $"Captive dependency: {parentLifetime} '{parentType.Name}' " +
            $"cannot depend on {dependencyLifetime} '{dependencyType.Name}'.");
    }
}
```

### Phase 5: Thread Safety Considerations

#### 5.1 Scope Thread Safety

The current design uses:
- `ConcurrentDictionary` for scoped instance cache (thread-safe reads/writes)
- `lock` for disposables list (protects during disposal)

This allows a scope to be used from multiple threads, though this is atypical. Most scope usage is single-threaded (one web request = one thread = one scope).

#### 5.2 Root Container Thread Safety

The existing `MagicDI` thread safety is preserved:
- Singletons use double-check locking (unchanged)
- `ThreadLocal` context stack (unchanged)

### Phase 6: Testing Strategy

#### 6.1 Unit Tests

```csharp
public class ScopedLifetimeTests
{
    [Fact]
    public void Scoped_instances_are_same_within_scope()
    {
        var di = new MagicDI();
        using var scope = di.CreateScope();

        var instance1 = scope.Resolve<ScopedService>();
        var instance2 = scope.Resolve<ScopedService>();

        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Scoped_instances_are_different_across_scopes()
    {
        var di = new MagicDI();

        ScopedService instance1, instance2;
        using (var scope1 = di.CreateScope())
        {
            instance1 = scope1.Resolve<ScopedService>();
        }
        using (var scope2 = di.CreateScope())
        {
            instance2 = scope2.Resolve<ScopedService>();
        }

        instance1.Should().NotBeSameAs(instance2);
    }

    [Fact]
    public void Disposing_scope_disposes_scoped_instances()
    {
        var di = new MagicDI();
        DisposableScopedService instance;

        using (var scope = di.CreateScope())
        {
            instance = scope.Resolve<DisposableScopedService>();
        }

        instance.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Disposing_scope_disposes_transient_instances()
    {
        var di = new MagicDI();
        DisposableTransient instance;

        using (var scope = di.CreateScope())
        {
            instance = scope.Resolve<DisposableTransient>();
        }

        instance.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Resolving_scoped_from_root_throws()
    {
        var di = new MagicDI();

        var act = () => di.Resolve<ScopedService>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CreateScope*");
    }

    [Fact]
    public void Singleton_depending_on_scoped_throws()
    {
        var di = new MagicDI();
        using var scope = di.CreateScope();

        var act = () => scope.Resolve<SingletonWithScopedDep>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Captive dependency*");
    }

    [Fact]
    public void Singletons_are_shared_across_scopes()
    {
        var di = new MagicDI();

        object instance1, instance2;
        using (var scope1 = di.CreateScope())
        {
            instance1 = scope1.Resolve<SingletonService>();
        }
        using (var scope2 = di.CreateScope())
        {
            instance2 = scope2.Resolve<SingletonService>();
        }

        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Disposed_scope_throws_on_resolve()
    {
        var di = new MagicDI();
        var scope = di.CreateScope();
        scope.Dispose();

        var act = () => scope.Resolve<ScopedService>();

        act.Should().Throw<ObjectDisposedException>();
    }

    // Test classes
    [Lifetime(Lifetime.Scoped)]
    public class ScopedService { }

    [Lifetime(Lifetime.Scoped)]
    public class DisposableScopedService : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    public class DisposableTransient : IDisposable
    {
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    [Lifetime(Lifetime.Singleton)]
    public class SingletonService { }

    [Lifetime(Lifetime.Singleton)]
    public class SingletonWithScopedDep(ScopedService scoped) { }
}
```

### Phase 7: Documentation Updates

#### 7.1 README Updates

Add to Lifetime Management section:

```markdown
### Scoped

A new instance is created for each scope. All resolutions within the same scope return the same instance:

\`\`\`csharp
[Lifetime(Lifetime.Scoped)]
public class DbContext : IDisposable { }

var di = new MagicDI();

using (var scope = di.CreateScope())
{
    var ctx1 = scope.Resolve<DbContext>();
    var ctx2 = scope.Resolve<DbContext>();
    // ctx1 and ctx2 are the same instance
} // DbContext is disposed here

using (var scope2 = di.CreateScope())
{
    var ctx3 = scope2.Resolve<DbContext>();
    // ctx3 is a different instance
}
\`\`\`

**Important**: Scoped types cannot be resolved from the root container - you must use `CreateScope()` first.
```

#### 7.2 Remove Limitation

Remove "No Scoped Lifetime" from the Limitations section.

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `IMagicScope.cs` | New | Public interface for scopes |
| `MagicScope.cs` | New | Internal scope implementation |
| `MagicDI.cs` | Modified | Add `CreateScope()`, modify resolution |
| `InstanceFactory.cs` | Modified | Pass scope through resolution |
| `LifetimeResolver.cs` | Modified | Add Scoped captive validation |
| `MagicDITests.Scoped.cs` | New | Scoped lifetime tests |
| `README.md` | Modified | Document scoped lifetime |

## Migration Notes

### Breaking Changes

None. Existing code continues to work unchanged.

### New Validation Errors

Users may see new captive dependency errors if they:
1. Mark a type as `[Lifetime(Lifetime.Singleton)]` that depends on a `[Lifetime(Lifetime.Scoped)]` type

This is correct behavior - such configurations are bugs that were previously undetectable.

## Future Considerations

### Async Disposal

.NET Core 3.0+ supports `IAsyncDisposable`. A future enhancement could add:

```csharp
public interface IMagicScope : IDisposable, IAsyncDisposable
{
    T Resolve<T>();
    ValueTask DisposeAsync();
}
```

### Nested Scopes

Some DI containers support nested scopes where child scopes inherit from parents. This adds complexity and is not included in this plan. If needed, it could be added later.

### Service Provider Compatibility

For ASP.NET Core integration, `MagicDI` could implement `IServiceProvider` and `IMagicScope` could implement `IServiceScope`. This is out of scope for this plan but would enable:

```csharp
builder.Services.AddMagicDI();
```

## Estimated Effort

| Phase | Effort |
|-------|--------|
| Phase 1: Core Infrastructure | Medium |
| Phase 2: Lifetime Resolution | Small |
| Phase 3: InstanceFactory Updates | Small |
| Phase 4: Captive Dependency Validation | Small |
| Phase 5: Thread Safety Review | Small |
| Phase 6: Testing | Medium |
| Phase 7: Documentation | Small |

## Open Questions

1. **Should `Dispose()` aggregate exceptions?** Current plan swallows them. Alternative: collect all exceptions and throw `AggregateException`.

2. **Should scoped resolution from root return transient instead of throwing?** Some containers do this. Current plan: throw with helpful message.

3. **Should we track singleton IDisposables for container disposal?** Out of scope for this plan, but related.
