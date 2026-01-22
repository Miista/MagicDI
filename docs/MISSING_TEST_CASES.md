# Missing Test Cases Analysis

This document identifies gaps in test coverage for the MagicDI library.

## High Priority (Core Functionality Gaps)

### 1. Scoped Lifetime - Remove Unused Enum

The `Lifetime.Scoped` enum value exists in `Lifetime.cs` but is never used or tested anywhere in the codebase.

**Action:** Remove the unused `Scoped` enum value from `Lifetime.cs`.

**Location:** `src/MagicDI/Lifetime.cs`

---

### 2. Value Types / Structs

No tests for value type resolution behavior:

| Type | Expected Behavior | Test Needed |
|------|-------------------|-------------|
| `DateTime` | Should fail (value type) | Yes |
| `Guid` | Should fail (value type) | Yes |
| `TimeSpan` | Should fail (value type) | Yes |
| Custom structs | Should fail | Yes |
| Enums | Should fail | Yes |

**Action:** Add value type check in `MagicDI.Resolve<T>()` method (not InstanceFactory, since it always goes through Resolve). Add tests for the above types.

**Location:** `MagicDITests.General.ErrorHandling`

---

### 3. Generic Type Resolution

No tests for generic type handling:

- `Resolve<List<MyClass>>()` - generic type with resolvable type argument
- Generic class implementations with dependencies
- Lifetime determination for generic types
- Open generic types `Resolve<List<>>()` - should fail

**Location:** New `MagicDITests.GenericTypes.cs` or extend `General`

---

### 4. Assembly/Implementation Discovery Edge Cases

The `ImplementationFinder` has untested error handling:

- `ReflectionTypeLoadException` is caught but behavior not tested
- Assembly load failures for referenced assemblies
- Implementation only in transitive referenced assembly
- Diamond dependency scenarios

**Location:** New `MagicDITests.ImplementationFinder.cs`

---

## Medium Priority (Edge Cases)

### 5. Constructor Selection Edge Cases

| Scenario | Current Coverage | Test Needed |
|----------|-----------------|-------------|
| Static class resolution | Not tested | Should fail with clear error |
| Protected/internal constructors | Not tested | Verify only public used |
| `ref`/`out` parameters | Not tested | Should fail gracefully |
| Default parameter values | Not tested | Verify behavior |
| Generic type constructors | Not tested | Verify selection works |

**Location:** `MagicDITests.General.ConstructorSelection`

---

### 6. Lifetime Attribute Edge Cases

- `[Lifetime]` attribute on interface (should be ignored, use implementation's)
- `[Lifetime]` on base class vs derived class (inheritance behavior)
- `[Lifetime]` on abstract class
- Multiple `[Lifetime]` attributes on same class

**Location:** `MagicDITests.Lifetimes.cs` - new `AttributeEdgeCases` nested class

---

### 7. Additional Primitive Types

Only `int` and `string` are explicitly tested for rejection. Missing:

- `byte`, `sbyte`
- `short`, `ushort`
- `long`, `ulong`
- `float`, `double`, `decimal`
- `bool`, `char`

**Location:** `MagicDITests.General.ErrorHandling`

---

### 8. Context Stack Cleanup on Exceptions

- Exception during dependency resolution - is context stack properly cleaned up?
- Deeply nested resolution (50+ levels) - stack overflow protection
- Container remains usable after various exception types

**Location:** `MagicDITests.CircularDependencies.Recovery` or new `ErrorRecovery` class

---

## Lower Priority (Defensive/Stress Tests)

### 9. Special Type Scenarios

| Type | Test Needed |
|------|-------------|
| C# record types | Verify generated constructor selection |
| Sealed classes | Should work normally |
| Nested public classes | Should be resolvable |
| Nested private classes | Should fail appropriately |
| Tuple types | Should fail |

**Location:** New `MagicDITests.SpecialTypes.cs`

---

### 10. Concurrency Edge Cases

- Multiple threads determining lifetime for same type simultaneously
- Captive dependency detection under concurrent load
- Context stack thread isolation for concrete types (only tested for interfaces currently)
- Race condition in singleton cache population

**Location:** `MagicDITests.ThreadSafety.cs`

---

### 11. Multiple Container Isolation

Explicit test that singletons are isolated per `MagicDI` instance - currently only implicit.

```csharp
[Fact]
public void Singletons_are_isolated_per_container_instance()
{
    var di1 = new MagicDI();
    var di2 = new MagicDI();

    var instance1 = di1.Resolve<SomeClass>();
    var instance2 = di2.Resolve<SomeClass>();

    instance1.Should().NotBeSameAs(instance2);
}
```

**Location:** `MagicDITests.General` or `MagicDITests.Lifetimes`

---

## Summary

| Category | Missing Tests | Priority |
|----------|--------------|----------|
| Scoped Lifetime | Implementation or removal | High |
| Value Types/Structs | ~5 tests | High |
| Generic Types | ~4 tests | High |
| Assembly Discovery | ~3 tests | High |
| Constructor Edge Cases | ~5 tests | Medium |
| Lifetime Attributes | ~4 tests | Medium |
| Additional Primitives | ~10 tests | Medium |
| Exception Recovery | ~3 tests | Medium |
| Special Types | ~5 tests | Low |
| Concurrency Edge Cases | ~4 tests | Low |
| Container Isolation | 1 test | Low |

**Estimated total missing tests:** ~45-50 test cases

---

## Recommended New Test Files

```
MagicDI.Tests/
├── (existing files)
├── MagicDITests.ImplementationFinder.cs (NEW)
├── MagicDITests.GenericTypes.cs (NEW)
├── MagicDITests.SpecialTypes.cs (NEW)
└── MagicDITests.ErrorRecovery.cs (NEW)
```

---

*Generated: January 2026*
