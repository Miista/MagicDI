# Critical Issues Remediation

Based on the code review, here are remediation options organized by priority.

---

## Critical Fixes

### 1. Thread Safety

**Option A: ConcurrentDictionary (Recommended)**
```csharp
private readonly ConcurrentDictionary<Type, InstanceRegistry> _registeredInstances = new();
```
- Simple drop-in replacement
- Need to use `GetOrAdd()` with a factory to ensure single instance creation

**Option B: Lock-based approach**
```csharp
private readonly object _lock = new();
private readonly Dictionary<Type, InstanceRegistry> _registeredInstances = new();

// In Resolve():
lock (_lock)
{
    // resolution logic
}
```
- More control but slightly more code
- Simpler to reason about for singleton guarantees

---

### 2. Lifetime Implementation

**Option A: Remove unused lifetimes**
- Delete `Transient` and `Scoped` from the enum
- Rename to clarify singleton-only behavior
- Simplest fix, honest about capabilities

**Option B: Implement all lifetimes**
- Add registration API: `Register<T>(Lifetime lifetime)`
- Store lifetime preference per type
- Transient: always create new instance
- Scoped: requires scope context (more complex)

**Option C: Implement Transient only**
- Add attribute-based lifetime: `[Transient]` on classes
- Read attribute in `DetermineLifeTime()`
- Defer Scoped to future version

---

### 3. Circular Dependency Detection

**Option A: Resolution stack tracking**
```csharp
private readonly ThreadLocal<HashSet<Type>> _resolutionStack = new(() => new HashSet<Type>());

private object ResolveInstance(Type type)
{
    if (!_resolutionStack.Value.Add(type))
        throw new InvalidOperationException($"Circular dependency detected: {type.Name}");

    try
    {
        // existing resolution logic
    }
    finally
    {
        _resolutionStack.Value.Remove(type);
    }
}
```

**Option B: Pass resolution chain as parameter**
- Pass `HashSet<Type>` through recursive calls
- Check before each resolution
- Slightly cleaner but changes method signatures

---

## High Priority Enhancements

### 4. Interface Registration

**Option A: Simple dictionary mapping**
```csharp
private readonly Dictionary<Type, Type> _typeMappings = new();

public void Register<TInterface, TImplementation>() where TImplementation : TInterface
{
    _typeMappings[typeof(TInterface)] = typeof(TImplementation);
}

// In Resolve: check _typeMappings first
```

**Option B: Registration with instance**
```csharp
public void RegisterInstance<T>(T instance)
{
    // Pre-cache the instance
}
```

---

### 5. Primitive/Value Support

**Option A: Factory registration**
```csharp
public void Register<T>(Func<T> factory)
{
    // Store and invoke factory when T is requested
}

// Usage:
di.Register(() => "connection-string");
di.Register(() => 5000); // port number
```

**Option B: Named registrations**
```csharp
public void Register<T>(string name, T value);
public T Resolve<T>(string name);
```

---

## Suggested Implementation Order

| Step | Task | Complexity |
|------|------|------------|
| 1 | Fix thread safety (ConcurrentDictionary) | Low |
| 2 | Add circular dependency detection | Low |
| 3 | Remove Scoped/Transient or implement Transient | Low-Medium |
| 4 | Add `Register<TInterface, TImpl>()` | Medium |
| 5 | Add factory registration | Medium |
| 6 | Add concurrency tests | Low |
| 7 | Implement Scoped lifetime | High |
