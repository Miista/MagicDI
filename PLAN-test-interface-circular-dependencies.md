# Implementation Plan: Interface-Based Circular Dependency Tests for MagicDI

## Overview

### Problem Statement

MagicDI currently has circular dependency tests that only cover concrete class scenarios. The existing 6 tests in `MagicDITests.CircularDependencies.cs` verify:
- Direct circular (A -> B -> A)
- Indirect circular (A -> B -> C -> A)
- Self-referencing (A -> A)
- Recovery after circular detection

However, with the interface resolution feature now implemented, there is a gap in test coverage. Circular dependencies can also occur through interface boundaries, and these scenarios need explicit testing to ensure:
1. Circular dependencies are correctly detected when interfaces are involved
2. Error messages contain helpful concrete type names (since detection happens after interface resolution)
3. The detection mechanism works correctly across mixed interface/concrete dependency chains

### Current Architecture Analysis

The circular dependency detection mechanism works as follows:

1. **In `LifetimeResolver.DetermineLifetime()`** (lines 54-120):
   - First converts interface to concrete type via `ImplementationFinder.GetConcreteType()` (line 57)
   - Then checks for circular dependency using `_lifetimeStack` (line 64)
   - The stack tracks **concrete types**, not interfaces

2. **In `InstanceFactory.CreateInstance()`** (lines 30-66):
   - Receives already-resolved concrete types from `MagicDI.Resolve()`
   - Checks for circular dependency using `_resolutionStack` (line 37)
   - The stack tracks **concrete types**

**Key Insight**: Since `ImplementationFinder.GetConcreteType()` is called BEFORE the circular dependency check, the detection always happens at the concrete type level. This means error messages will contain concrete type names, not interface names.

---

## Test Class Definitions

### File: `MagicDITests.InterfaceCircularDependencies.cs`

```csharp
using System;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceCircularDependency
        {
            #region Test Interfaces and Classes

            // Scenario 1: Direct Interface Circular
            public interface IServiceA { void DoWork(); }
            public interface IServiceB { void DoWork(); }

            public class ServiceA : IServiceA
            {
                public IServiceB ServiceB { get; }
                public ServiceA(IServiceB serviceB) { ServiceB = serviceB; }
                public void DoWork() { }
            }

            public class ServiceB : IServiceB
            {
                public IServiceA ServiceA { get; }
                public ServiceB(IServiceA serviceA) { ServiceA = serviceA; }
                public void DoWork() { }
            }

            // Scenario 2: Mixed Interface/Concrete Circular
            public interface IMixedService { void DoWork(); }

            public class MixedConcreteClass
            {
                public IMixedService Service { get; }
                public MixedConcreteClass(IMixedService service) { Service = service; }
            }

            public class MixedServiceImpl : IMixedService
            {
                public MixedConcreteClass Concrete { get; }
                public MixedServiceImpl(MixedConcreteClass concrete) { Concrete = concrete; }
                public void DoWork() { }
            }

            // Scenario 3: Self-Referencing Through Interface
            public interface ISelfReferencing { void DoSomething(); }

            public class SelfReferencingImpl : ISelfReferencing
            {
                public ISelfReferencing Self { get; }
                public SelfReferencingImpl(ISelfReferencing self) { Self = self; }
                public void DoSomething() { }
            }

            // Scenario 4: Three-Way Interface Circular
            public interface IAlpha { void Alpha(); }
            public interface IBeta { void Beta(); }
            public interface IGamma { void Gamma(); }

            public class AlphaImpl : IAlpha
            {
                public IBeta Beta { get; }
                public AlphaImpl(IBeta beta) { Beta = beta; }
                public void Alpha() { }
            }

            public class BetaImpl : IBeta
            {
                public IGamma Gamma { get; }
                public BetaImpl(IGamma gamma) { Gamma = gamma; }
                public void Beta() { }
            }

            public class GammaImpl : IGamma
            {
                public IAlpha Alpha { get; }
                public GammaImpl(IAlpha alpha) { Alpha = alpha; }
                public void Gamma() { }
            }

            // Scenario 5: Concrete Entry to Circular Interface Chain
            public interface IChainStart { void Start(); }
            public interface IChainEnd { void End(); }

            public class ConcreteEntry
            {
                public IChainStart ChainStart { get; }
                public ConcreteEntry(IChainStart chainStart) { ChainStart = chainStart; }
            }

            public class ChainStartImpl : IChainStart
            {
                public IChainEnd ChainEnd { get; }
                public ChainStartImpl(IChainEnd chainEnd) { ChainEnd = chainEnd; }
                public void Start() { }
            }

            public class ChainEndImpl : IChainEnd
            {
                public IChainStart ChainStart { get; }
                public ChainEndImpl(IChainStart chainStart) { ChainStart = chainStart; }
                public void End() { }
            }

            // Non-Circular Control
            public interface INonCircularService { void Work(); }
            public class NonCircularServiceImpl : INonCircularService { public void Work() { } }
            public class NonCircularConsumer
            {
                public INonCircularService Service { get; }
                public NonCircularConsumer(INonCircularService service) { Service = service; }
            }

            #endregion
        }
    }
}
```

---

## Test Scenarios

### Test 1: Direct Interface Circular Dependency

```csharp
[Fact]
public void Throws_when_direct_interface_circular_dependency_detected()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IServiceA>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "interface-based circular dependencies must be detected")
        .WithMessage("*circular*",
            because: "the error message should indicate a circular dependency");
}
```

### Test 2: Error Contains Concrete Types

```csharp
[Fact]
public void Error_message_contains_concrete_type_names_not_interfaces()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IServiceA>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*ServiceA*",
            because: "the error should mention the concrete implementation name")
        .WithMessage("*ServiceB*",
            because: "all concrete types in the circular chain should be mentioned");
}
```

### Test 3: Mixed Interface/Concrete Circular

```csharp
[Fact]
public void Throws_when_mixed_interface_concrete_circular_dependency()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<MixedConcreteClass>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "circular dependencies between concrete classes and interface implementations must be detected")
        .WithMessage("*circular*");
}
```

### Test 4: Mixed Circular from Interface Entry

```csharp
[Fact]
public void Throws_when_mixed_circular_resolved_via_interface()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IMixedService>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "the same circular dependency should be detected regardless of entry point")
        .WithMessage("*MixedServiceImpl*");
}
```

### Test 5: Self-Referencing Through Interface

```csharp
[Fact]
public void Throws_when_self_referencing_through_interface()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<ISelfReferencing>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "self-referencing through an interface is still a circular dependency")
        .WithMessage("*SelfReferencingImpl*");
}
```

### Test 6: Three-Way Interface Circular

```csharp
[Fact]
public void Throws_when_three_way_interface_circular_dependency()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IAlpha>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "multi-hop interface circular dependencies must be detected")
        .WithMessage("*circular*");
}
```

### Test 7: Three-Way Error Contains Full Chain

```csharp
[Fact]
public void Three_way_circular_error_includes_full_chain()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IAlpha>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*AlphaImpl*",
            because: "the first type in the chain should be mentioned")
        .And.Message.Should().ContainAny("BetaImpl", "GammaImpl",
            "because the chain should include intermediate types");
}
```

### Test 8: Concrete Entry to Circular Interface Chain

```csharp
[Fact]
public void Throws_when_concrete_depends_on_circular_interface_chain()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<ConcreteEntry>();

    // Assert
    act.Should().Throw<InvalidOperationException>(
            because: "circular dependencies in interface chains should be detected even when entry point is concrete")
        .WithMessage("*circular*");
}
```

### Test 9: Container Remains Usable After Detection

```csharp
[Fact]
public void Remains_usable_after_interface_circular_detection()
{
    // Arrange
    var di = new MagicDI();

    // Act - trigger circular dependency
    Action failedResolution = () => di.Resolve<IServiceA>();
    failedResolution.Should().Throw<InvalidOperationException>();

    // Act - resolve a valid type
    var instance = di.Resolve<NonCircularConsumer>();

    // Assert
    instance.Should().NotBeNull();
    instance.Service.Should().NotBeNull();
}
```

### Test 10: Non-Circular Control Test

```csharp
[Fact]
public void Resolves_non_circular_interface_dependencies_successfully()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = di.Resolve<NonCircularConsumer>();

    // Assert
    instance.Should().NotBeNull();
    instance.Service.Should().BeOfType<NonCircularServiceImpl>();
}
```

### Test 11: Detection Consistency

```csharp
[Fact]
public void Detects_same_circular_dependency_from_either_interface()
{
    // Arrange
    var di1 = new MagicDI();
    var di2 = new MagicDI();

    // Act
    Action resolveA = () => di1.Resolve<IServiceA>();
    Action resolveB = () => di2.Resolve<IServiceB>();

    // Assert
    resolveA.Should().Throw<InvalidOperationException>();
    resolveB.Should().Throw<InvalidOperationException>();
}
```

---

## Implementation Steps

1. **Create Test File**: Create `/home/user/MagicDI/src/MagicDI.Tests/MagicDITests.InterfaceCircularDependencies.cs`

2. **Add Test Classes**: Include all interface and class definitions in the `#region Test Interfaces and Classes` section

3. **Implement Tests 1-5**: Core detection tests for direct, mixed, and self-referencing scenarios

4. **Implement Tests 6-8**: Multi-hop and concrete entry point tests

5. **Implement Tests 9-11**: Recovery and control tests

6. **Run Tests**:
   ```bash
   dotnet test src/MagicDI.sln --filter "FullyQualifiedName~InterfaceCircularDependency"
   ```

---

## Expected Error Message Format

Based on the implementation in `LifetimeResolver.cs`, error messages follow this format:

```
Circular dependency detected while resolving {TypeName}. Resolution chain: {Type1} -> {Type2} -> ... -> {TypeName}
```

For example, resolving `IServiceA` should produce:
```
Circular dependency detected while resolving ServiceA. Resolution chain: ServiceA -> ServiceB -> ServiceA
```

Note: The chain shows concrete types (ServiceA, ServiceB), not interfaces (IServiceA, IServiceB).

---

## Summary

| Test | Scenario | Key Verification |
|------|----------|------------------|
| 1 | Direct interface circular | Detection works |
| 2 | Error message contents | Concrete type names appear |
| 3 | Mixed interface/concrete | Detection works |
| 4 | Mixed from interface | Entry point independence |
| 5 | Self-referencing via interface | Edge case handling |
| 6 | Three-way interface circular | Long chains detected |
| 7 | Three-way error contents | Full chain in message |
| 8 | Concrete to interface chain | Nested interface circular |
| 9 | Container recovery | Thread-local cleanup |
| 10 | Non-circular control | No false positives |
| 11 | Detection consistency | Both entry points work |
