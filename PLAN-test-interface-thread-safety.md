# Implementation Plan: Interface-Specific Thread Safety Tests for MagicDI

## 1. Overview

### Problem Statement

MagicDI's thread safety testing currently covers only concrete type resolution (4 tests in `MagicDITests.ThreadSafety.cs`). The addition of interface resolution introduced new concurrency concerns that are not tested:

1. **Context Stack (`_contextStack`)**: Thread-local stack tracking the current requesting type for interface resolution
2. **Interface-to-Concrete Mapping**: Assembly scanning via `ImplementationFinder.GetConcreteType()`
3. **Lifetime Resolution with Interfaces**: `LifetimeResolver.DetermineLifetime()` must resolve interfaces to concrete types

### Current Thread Safety Mechanisms

| Component | Mechanism | Purpose |
|-----------|-----------|---------|
| `MagicDI._singletons` | `ConcurrentDictionary<Type, object>` | Thread-safe singleton cache |
| `MagicDI._singletonLock` | `lock` object | Double-check locking for singleton creation |
| `MagicDI._contextStack` | `ThreadLocal<Stack<Type>>` | Thread-isolated interface resolution context |
| `LifetimeResolver._lifetimes` | `ConcurrentDictionary<Type, Lifetime>` | Thread-safe lifetime cache |
| `LifetimeResolver._lifetimeStack` | `ThreadLocal<HashSet<Type>>` | Thread-isolated circular detection |
| `InstanceFactory._resolutionStack` | `ThreadLocal<HashSet<Type>>` | Thread-isolated circular detection |

---

## 2. Priority Ranking

### Priority 1: Critical (Must Have)

| Rank | Scenario | Rationale |
|------|----------|-----------|
| P1.1 | Concurrent interface resolution with singleton verification | Core DI guarantee |
| P1.2 | Context stack isolation during concurrent interface resolution | Thread contamination prevention |
| P1.3 | Nested interface dependencies with concurrent access | Full resolution path testing |

### Priority 2: Important (Should Have)

| Rank | Scenario | Rationale |
|------|----------|-----------|
| P2.1 | Concurrent mixed concrete/interface resolution | Real-world usage pattern |
| P2.2 | Lifetime determination concurrency for interfaces | Cache population under load |
| P2.3 | Concurrent interface resolution with transient dependencies | Transient instance creation |

### Priority 3: Defensive (Nice to Have)

| Rank | Scenario | Rationale |
|------|----------|-----------|
| P3.1 | Ambiguity detection under concurrent load | Error path consistency |
| P3.2 | Assembly resolution stability under concurrent load | ImplementationFinder stability |
| P3.3 | Captive dependency detection with concurrent interface access | Error detection with interfaces |

---

## 3. Test Implementations

### File: `src/MagicDI.Tests/MagicDITests.InterfaceThreadSafety.cs`

```csharp
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceThreadSafety
        {
            // Test classes defined below
        }
    }
}
```

### Priority 1 Tests

#### P1.1: Concurrent Interface Resolution with Singleton Verification

```csharp
public class InterfaceSingletonGuarantee
{
    [Fact]
    public async Task Concurrent_interface_resolves_return_same_singleton_instance()
    {
        // Arrange
        const int threadCount = 10;

        var di = new MagicDI();
        var instances = new ConcurrentBag<IThreadSafeService>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var instance = di.Resolve<IThreadSafeService>();
            instances.Add(instance);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var uniqueInstances = instances.Distinct().Count();
        uniqueInstances.Should().Be(1,
            because: "all threads should receive the same singleton instance when resolving via interface");
    }

    [Fact]
    public async Task Interface_singleton_constructor_is_called_exactly_once()
    {
        // Arrange
        const int threadCount = 20;

        InterfaceInstanceCountingService.ResetCounter();
        var di = new MagicDI();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            di.Resolve<IInstanceCountingService>();
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        InterfaceInstanceCountingService.InstanceCount.Should().Be(1,
            because: "singleton constructor must only be invoked once");
    }
}
```

#### P1.2: Context Stack Isolation

```csharp
public class ContextStackIsolation
{
    [Fact]
    public async Task Context_stacks_are_isolated_between_threads()
    {
        // Arrange
        const int threadCount = 10;

        var di = new MagicDI();
        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);
        var results = new ConcurrentBag<INestedInterfaceService>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                var instance = di.Resolve<INestedInterfaceService>();
                results.Add(instance);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty(
            because: "context stack isolation should prevent cross-thread contamination");
        results.Should().HaveCount(threadCount);
    }

    [Fact]
    public async Task Nested_interface_resolution_maintains_correct_context_per_thread()
    {
        // Arrange
        const int threadCount = 50;

        var di = new MagicDI();
        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                var instance = di.Resolve<IDeepInterfaceLevel3>();
                instance.Should().NotBeNull();
                instance.Level2.Should().NotBeNull();
                instance.Level2.Level1.Should().NotBeNull();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty(
            because: "deeply nested interface resolution should work correctly under concurrent load");
    }
}
```

#### P1.3: Nested Interface Dependencies

```csharp
public class NestedInterfaceConcurrency
{
    [Fact]
    public async Task Nested_interface_singleton_dependencies_are_shared_across_threads()
    {
        // Arrange
        const int threadCount = 10;

        var di = new MagicDI();
        var sharedDependencies = new ConcurrentBag<ISharedInterfaceDependency>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var parent = di.Resolve<IParentWithInterfaceDependency>();
            sharedDependencies.Add(parent.Dependency);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var uniqueDependencies = sharedDependencies.Distinct().Count();
        uniqueDependencies.Should().Be(1,
            because: "all parents should share the same singleton interface dependency");
    }

    [Fact]
    public async Task Interface_resolution_chain_is_thread_safe()
    {
        // Arrange
        const int threadCount = 20;

        var di = new MagicDI();
        var results = new ConcurrentBag<IInterfaceChainEnd>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var instance = di.Resolve<IInterfaceChainEnd>();
            results.Add(instance);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var uniqueInstances = results.Distinct().Count();
        uniqueInstances.Should().Be(1,
            because: "interface chain resolution should maintain singleton semantics");
    }
}
```

### Priority 2 Tests

#### P2.1: Mixed Concrete/Interface Resolution

```csharp
public class MixedResolution
{
    [Fact]
    public async Task Concurrent_mixed_interface_and_concrete_resolution_works()
    {
        // Arrange
        const int threadCount = 100;

        var di = new MagicDI();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            try
            {
                if (i % 2 == 0)
                    di.Resolve<IThreadSafeService>();
                else
                    di.Resolve<ThreadSafeServiceImpl>();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty(
            because: "mixed interface and concrete resolution should be thread-safe");
    }

    [Fact]
    public async Task Interface_and_concrete_resolve_to_same_singleton()
    {
        // Arrange
        const int threadCount = 20;

        var di = new MagicDI();
        var interfaceInstances = new ConcurrentBag<IThreadSafeService>();
        var concreteInstances = new ConcurrentBag<ThreadSafeServiceImpl>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();
            if (i % 2 == 0)
                interfaceInstances.Add(di.Resolve<IThreadSafeService>());
            else
                concreteInstances.Add(di.Resolve<ThreadSafeServiceImpl>());
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var allInstances = interfaceInstances.Cast<object>()
            .Concat(concreteInstances.Cast<object>())
            .Distinct()
            .Count();

        allInstances.Should().Be(1,
            because: "interface and concrete resolution should return the same singleton");
    }
}
```

#### P2.2: Lifetime Determination Concurrency

```csharp
public class LifetimeDeterminationConcurrency
{
    [Fact]
    public async Task Concurrent_lifetime_determination_for_interfaces_is_consistent()
    {
        // Arrange
        const int threadCount = 50;

        var di = new MagicDI();
        var results = new ConcurrentBag<bool>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var instance1 = di.Resolve<ITransientInterfaceService>();
            var instance2 = di.Resolve<ITransientInterfaceService>();
            var isTransient = !ReferenceEquals(instance1, instance2);
            results.Add(isTransient);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        results.All(r => r).Should().BeTrue(
            because: "lifetime determination should be consistent across threads");
    }
}
```

#### P2.3: Transient Interface Concurrency

```csharp
public class TransientInterfaceConcurrency
{
    [Fact]
    public async Task Transient_interface_creates_new_instance_per_resolution()
    {
        // Arrange
        const int threadCount = 10;
        const int resolutionsPerThread = 5;

        var di = new MagicDI();
        var instances = new ConcurrentBag<ITransientInterfaceService>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < resolutionsPerThread; i++)
            {
                instances.Add(di.Resolve<ITransientInterfaceService>());
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var uniqueInstances = instances.Distinct().Count();
        uniqueInstances.Should().Be(threadCount * resolutionsPerThread,
            because: "transient interface resolution should create a new instance each time");
    }
}
```

### Priority 3 Tests

#### P3.1: Ambiguity Detection Under Load

```csharp
public class AmbiguityDetectionConcurrency
{
    [Fact]
    public async Task Multiple_implementations_throws_consistently_under_concurrent_load()
    {
        // Arrange
        const int threadCount = 20;

        var di = new MagicDI();
        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                di.Resolve<IAmbiguousInterface>();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().HaveCount(threadCount,
            because: "all threads should receive the ambiguity exception");
        exceptions.All(e => e is InvalidOperationException).Should().BeTrue();
        exceptions.All(e => e.Message.Contains("Multiple implementations")).Should().BeTrue();
    }
}
```

---

## 4. Test Helper Classes

```csharp
#region Test Interfaces and Implementations

// Basic singleton interface
public interface IThreadSafeService { void DoWork(); }
public class ThreadSafeServiceImpl : IThreadSafeService { public void DoWork() { } }

// Instance counting for constructor verification
public interface IInstanceCountingService { int GetCount(); }
public class InterfaceInstanceCountingService : IInstanceCountingService
{
    private static int _instanceCount;
    public static int InstanceCount => _instanceCount;
    public InterfaceInstanceCountingService() { Interlocked.Increment(ref _instanceCount); }
    public static void ResetCounter() => _instanceCount = 0;
    public int GetCount() => InstanceCount;
}

// Nested interface dependencies
public interface ISharedInterfaceDependency { }
public class SharedInterfaceDependencyImpl : ISharedInterfaceDependency { }

public interface IParentWithInterfaceDependency { ISharedInterfaceDependency Dependency { get; } }
public class ParentWithInterfaceDependencyImpl : IParentWithInterfaceDependency
{
    public ISharedInterfaceDependency Dependency { get; }
    public ParentWithInterfaceDependencyImpl(ISharedInterfaceDependency dep) { Dependency = dep; }
}

public interface INestedInterfaceService { ISharedInterfaceDependency InnerService { get; } }
public class NestedInterfaceServiceImpl : INestedInterfaceService
{
    public ISharedInterfaceDependency InnerService { get; }
    public NestedInterfaceServiceImpl(ISharedInterfaceDependency inner) { InnerService = inner; }
}

// Deep interface chain
public interface IDeepInterfaceLevel1 { }
public class DeepInterfaceLevel1Impl : IDeepInterfaceLevel1 { }

public interface IDeepInterfaceLevel2 { IDeepInterfaceLevel1 Level1 { get; } }
public class DeepInterfaceLevel2Impl : IDeepInterfaceLevel2
{
    public IDeepInterfaceLevel1 Level1 { get; }
    public DeepInterfaceLevel2Impl(IDeepInterfaceLevel1 l1) { Level1 = l1; }
}

public interface IDeepInterfaceLevel3 { IDeepInterfaceLevel2 Level2 { get; } }
public class DeepInterfaceLevel3Impl : IDeepInterfaceLevel3
{
    public IDeepInterfaceLevel2 Level2 { get; }
    public DeepInterfaceLevel3Impl(IDeepInterfaceLevel2 l2) { Level2 = l2; }
}

// Interface chain
public interface IInterfaceChainStart { }
public class InterfaceChainStartImpl : IInterfaceChainStart { }

public interface IInterfaceChainMiddle { IInterfaceChainStart Start { get; } }
public class InterfaceChainMiddleImpl : IInterfaceChainMiddle
{
    public IInterfaceChainStart Start { get; }
    public InterfaceChainMiddleImpl(IInterfaceChainStart s) { Start = s; }
}

public interface IInterfaceChainEnd { IInterfaceChainMiddle Middle { get; } }
public class InterfaceChainEndImpl : IInterfaceChainEnd
{
    public IInterfaceChainMiddle Middle { get; }
    public InterfaceChainEndImpl(IInterfaceChainMiddle m) { Middle = m; }
}

// Transient interface (IDisposable implementation)
public interface ITransientInterfaceService { void DoWork(); }
public class TransientInterfaceServiceImpl : ITransientInterfaceService, IDisposable
{
    public void DoWork() { }
    public void Dispose() { }
}

// Ambiguous interface (multiple implementations)
public interface IAmbiguousInterface { void DoSomething(); }
public class AmbiguousImplA : IAmbiguousInterface { public void DoSomething() { } }
public class AmbiguousImplB : IAmbiguousInterface { public void DoSomething() { } }

#endregion
```

---

## 5. Implementation Steps

1. **Create file**: `src/MagicDI.Tests/MagicDITests.InterfaceThreadSafety.cs`

2. **Add test helper classes** in `#region Test Interfaces and Implementations`

3. **Implement Priority 1 tests** (6 tests):
   - `InterfaceSingletonGuarantee` (2 tests)
   - `ContextStackIsolation` (2 tests)
   - `NestedInterfaceConcurrency` (2 tests)

4. **Implement Priority 2 tests** (4 tests):
   - `MixedResolution` (2 tests)
   - `LifetimeDeterminationConcurrency` (1 test)
   - `TransientInterfaceConcurrency` (1 test)

5. **Implement Priority 3 tests** (1 test):
   - `AmbiguityDetectionConcurrency` (1 test)

6. **Run tests**:
   ```bash
   dotnet test src/MagicDI.sln --filter "FullyQualifiedName~InterfaceThreadSafety"
   ```

---

## 6. Coverage Summary

| Component | Thread Safety Aspect | Test Coverage |
|-----------|---------------------|---------------|
| `MagicDI._contextStack` | Thread-local isolation | P1.2 |
| `MagicDI._singletons` | ConcurrentDictionary access | P1.1, P2.1 |
| `MagicDI._singletonLock` | Double-check locking | P1.1 |
| `LifetimeResolver._lifetimes` | ConcurrentDictionary access | P2.2 |
| `ImplementationFinder` | Stateless reflection | P3.2 |
| Error paths | Consistent exceptions | P3.1 |
