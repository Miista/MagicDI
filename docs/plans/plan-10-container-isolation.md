# Plan 10: Multiple Container Isolation

## Overview
Verify singletons are isolated per `MagicDI` container instance.

## Current State Analysis

**All singleton-related state is instance-based, not static:**

| Field | Location | Isolation |
|-------|----------|-----------|
| `_singletons` | MagicDI.cs:17 | Instance-level |
| `_singletonLock` | MagicDI.cs:16 | Instance-level |
| `_lifetimes` | LifetimeResolver.cs:20 | Instance-level |

**Conclusion:** Container isolation already works correctly.

## Test Scenarios

### 1. Singletons Are Isolated
```csharp
[Fact]
public void Singletons_are_isolated_per_container_instance()
{
    var container1 = new MagicDI();
    var container2 = new MagicDI();

    var instance1 = container1.Resolve<IsolatedSingleton>();
    var instance2 = container2.Resolve<IsolatedSingleton>();

    instance1.Should().NotBeSameAs(instance2);
}
```

### 2. Same Container Returns Same Singleton
```csharp
[Fact]
public void Same_container_returns_same_singleton()
{
    var container = new MagicDI();
    var instance1 = container.Resolve<IsolatedSingleton>();
    var instance2 = container.Resolve<IsolatedSingleton>();

    instance1.Should().BeSameAs(instance2);
}
```

### 3. Nested Dependencies Are Isolated
### 4. Lifetime Determination Is Isolated
### 5. Transient Types Create New Instances Regardless

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI.Tests/MagicDITests.Lifetimes.cs` | Add `ContainerIsolation` nested class |

## Expected Outcome
All 5 tests should pass on first run - implementation already supports isolation.

## Risk Level: Low
Test-only changes. Implementation already works correctly.
