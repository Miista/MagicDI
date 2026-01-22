# Plan 06: Lifetime Attribute Edge Cases

## Overview
Test and fix lifetime attribute inheritance behavior.

## Current State

**LifetimeAttribute.cs (Line 8):**
```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
```

**Problem:** `Inherited = false` prevents attribute inheritance from base classes.

## Proposed Change

Change to:
```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
```

This enables:
- Derived class attribute checked first
- Walks up inheritance chain if not found
- "Closest to concrete type" wins automatically

## Test Scenarios

### 1. `[Lifetime]` on Interface (Should Be Ignored)
```csharp
[Lifetime(Lifetime.Transient)]  // Ignored - interfaces not supported
public interface IService { }
public class ServiceImpl : IService { }  // Will be Singleton (default)
```
Note: `AttributeTargets.Class` prevents applying to interfaces at compile time.

### 2. `[Lifetime]` on Base vs Derived (Derived Wins)
```csharp
[Lifetime(Lifetime.Transient)]
public class BaseService { }

[Lifetime(Lifetime.Singleton)]  // Wins - closest to concrete
public class DerivedService : BaseService { }
```

### 3. Inherits from Base When Not Specified
```csharp
[Lifetime(Lifetime.Singleton)]
public class BaseService { }

public class DerivedService : BaseService { }  // Inherits Singleton
```

### 4. `[Lifetime]` on Abstract Class
```csharp
[Lifetime(Lifetime.Transient)]
public abstract class AbstractService { }

public class ConcreteService : AbstractService { }  // Inherits Transient
```

### 5. Multiple `[Lifetime]` Attributes
Compiler error - enforced by `AllowMultiple = false` (default).

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI/LifetimeAttribute.cs` | Change `Inherited = false` to `Inherited = true` |
| `src/MagicDI.Tests/MagicDITests.Lifetimes.cs` | Add `AttributeEdgeCases` nested class |

## Implementation Steps

1. Update `LifetimeAttribute.cs` line 8
2. Add test class with 4-5 test methods
3. Run tests - all should pass
4. Verify no breaking changes to existing tests
