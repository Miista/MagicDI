# Plan 09: Concurrency Edge Cases

## Overview
Test thread safety mechanisms under concurrent load.

## Current Thread Safety Mechanisms

### MagicDI.cs
- `_singletonLock` - Global lock for singleton creation
- `ConcurrentDictionary<Type, object> _singletons` - Thread-safe cache
- `ThreadLocal<Stack<Type>> _contextStack` - Per-thread isolation

### LifetimeResolver.cs
- `ConcurrentDictionary<Type, Lifetime> _lifetimes` - Thread-safe cache
- `ThreadLocal<HashSet<Type>> _lifetimeStack` - Per-thread isolation

### InstanceFactory.cs
- `ThreadLocal<HashSet<Type>> _resolutionStack` - Per-thread isolation

## Test Scenarios

### 1. Concurrent Lifetime Determination
```csharp
[Fact]
public async Task Concurrent_lifetime_determination_is_consistent()
{
    const int threadCount = 50;
    var di = new MagicDI();
    var barrier = new Barrier(threadCount);
    var results = new ConcurrentBag<bool>();

    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
    {
        barrier.SignalAndWait();
        var i1 = di.Resolve<TestClass>();
        var i2 = di.Resolve<TestClass>();
        results.Add(ReferenceEquals(i1, i2));  // Singleton check
    })).ToArray();

    await Task.WhenAll(tasks);
    results.All(r => r).Should().BeTrue();
}
```

### 2. Captive Dependency Under Concurrent Load
All threads should receive consistent `InvalidOperationException`.

### 3. Context Stack Thread Isolation
Deep dependency chains resolved concurrently shouldn't interfere.

### 4. Singleton Cache Race Condition
```csharp
[Fact]
public async Task Singleton_constructor_invoked_exactly_once()
{
    const int threadCount = 100;
    CountingSingleton.Reset();
    var di = new MagicDI();
    var barrier = new Barrier(threadCount);

    var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
    {
        barrier.SignalAndWait();
        di.Resolve<CountingSingleton>();
    })).ToArray();

    await Task.WhenAll(tasks);
    CountingSingleton.InstanceCount.Should().Be(1);
}
```

### 5. Additional Scenarios
- Circular dependency detection under concurrent load
- Exception during singleton creation releases lock
- Mixed singleton/transient resolution

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI.Tests/MagicDITests.ThreadSafety.cs` | Add `ConcurrencyEdgeCases` nested class |

## Estimated Test Count: 8 tests
