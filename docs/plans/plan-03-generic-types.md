# Plan 03: Generic Type Resolution

## Overview
Document and test generic type handling. **Deferred until IEnumerable<T> feature.**

## Current State
**No special handling exists.** No usage of `Type.IsGenericType`, `GetGenericTypeDefinition()`, etc.

### Current Behavior

| Scenario | Current Behavior |
|----------|------------------|
| `Resolve<List<MyClass>>()` | May fail trying to resolve `int` or `IEnumerable<T>` constructor params |
| `Resolve<MyGeneric<Dep>>()` | May work if constructor params are resolvable |
| `Resolve<List<>>()` (open generic) | Undefined behavior - likely runtime exception |

## Test Scenarios Needed

### Basic Generic Resolution
- `Resolve_ClosedGenericWithResolvableDependency_Succeeds`
- `Resolve_OpenGeneric_ThrowsInvalidOperationException`

### Lifetime for Generic Types
- `Lifetime_ClosedGenericWithAttribute_ReadsAttribute`
- `Lifetime_SameOpenGenericDifferentTypeArgs_IndependentLifetimes`

## Implementation Phases

### Phase 1: Open Generic Rejection (Pre-IEnumerable)
Add check in `InstanceFactory.CreateInstance()`:
```csharp
if (type.IsGenericTypeDefinition)
    throw new InvalidOperationException(
        $"Cannot resolve open generic type {type.Name}. " +
        $"Use a closed generic like {type.Name}<SomeType> instead.");
```

### Phase 2: Document Current Behavior
Write tests capturing what works today without changes.

### Phase 3: Enhanced Support (Post-IEnumerable)
Leverage IEnumerable infrastructure for better generic handling.

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI/InstanceFactory.cs` | Add open generic check |
| `src/MagicDI.Tests/MagicDITests.GenericTypes.cs` | New test file |

## Status
**Deferred until IEnumerable<T> implementation**
