# Implementation Plan: Abstract Class Edge Case Tests for MagicDI

## Overview

### Problem Statement

MagicDI currently has comprehensive test coverage for interface resolution (9 tests) but only a single happy-path test for abstract class resolution (`Resolves_abstract_class_to_implementation`). Since `ImplementationFinder` treats interfaces and abstract classes identically (using `IsAssignableFrom`), abstract classes should have equivalent test coverage.

### Current State

| Category | Interface Tests | Abstract Class Tests |
|----------|-----------------|---------------------|
| Basic resolution | ✅ | ✅ |
| No implementation error | ✅ | ❌ |
| Multiple implementations error | ✅ | ❌ |
| Constructor dependency | ✅ | ❌ |
| Nested dependencies | ✅ | ❌ |
| Lifetime determination | ✅ | ❌ |
| Disposable transient | ✅ | ❌ |
| Singleton sharing | ✅ | ❌ |

---

## Test Helper Classes

Add these to `MagicDITests.InterfaceResolution.cs` in the `#region Test Interfaces and Classes` section:

```csharp
// Abstract class with no implementations (for error testing)
public abstract class AbstractServiceWithNoImplementation
{
    public abstract void DoWork();
}

// Abstract class with multiple implementations (for ambiguity error testing)
public abstract class AbstractServiceWithMultipleImpls
{
    public abstract void DoWork();
}

public class ConcreteServiceImplA : AbstractServiceWithMultipleImpls
{
    public override void DoWork() { }
}

public class ConcreteServiceImplB : AbstractServiceWithMultipleImpls
{
    public override void DoWork() { }
}

// Abstract class for dependency injection testing
public abstract class AbstractRepository
{
    public abstract string GetData();
}

public class ConcreteRepository : AbstractRepository
{
    public override string GetData() => "data";
}

// Class that depends on an abstract class
public class ClassWithAbstractDependency
{
    public AbstractRepository Repository { get; }

    public ClassWithAbstractDependency(AbstractRepository repository)
    {
        Repository = repository;
    }
}

// Class with nested abstract class dependency
public class ClassWithNestedAbstractDependency
{
    public ClassWithAbstractDependency Wrapper { get; }

    public ClassWithNestedAbstractDependency(ClassWithAbstractDependency wrapper)
    {
        Wrapper = wrapper;
    }
}

// Disposable abstract class for lifetime testing
public abstract class DisposableAbstractService
{
    public abstract void DoWork();
}

public class DisposableConcreteService : DisposableAbstractService, IDisposable
{
    public override void DoWork() { }
    public void Dispose() { }
}
```

---

## Test Methods

### Test 1: Throws_when_no_abstract_implementation_exists

```csharp
[Fact]
public void Throws_when_no_abstract_implementation_exists()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<AbstractServiceWithNoImplementation>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the abstract class")
        .WithMessage("*No implementation found*", because: "the error message should explain the problem");
}
```

### Test 2: Throws_when_multiple_implementations_of_abstract_class_exist

```csharp
[Fact]
public void Throws_when_multiple_implementations_of_abstract_class_exist()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<AbstractServiceWithMultipleImpls>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
        .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity");
}
```

### Test 3: Resolves_abstract_class_dependency_in_constructor

```csharp
[Fact]
public void Resolves_abstract_class_dependency_in_constructor()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = di.Resolve<ClassWithAbstractDependency>();

    // Assert
    instance.Should().NotBeNull(because: "the container should create the class");
    instance.Repository.Should().NotBeNull(because: "the container should resolve abstract class dependencies");
    instance.Repository.Should().BeOfType<ConcreteRepository>(because: "the abstract class should resolve to its implementation");
}
```

### Test 4: Resolves_nested_abstract_class_dependencies

```csharp
[Fact]
public void Resolves_nested_abstract_class_dependencies()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = di.Resolve<ClassWithNestedAbstractDependency>();

    // Assert
    instance.Should().NotBeNull(because: "the container should create the top-level class");
    instance.Wrapper.Should().NotBeNull(because: "the container should resolve the wrapper");
    instance.Wrapper.Repository.Should().NotBeNull(because: "the container should resolve nested abstract class dependencies");
}
```

### Test 5: Determines_lifetime_from_abstract_class_implementation

```csharp
[Fact]
public void Determines_lifetime_from_abstract_class_implementation()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance1 = di.Resolve<AbstractService>();
    var instance2 = di.Resolve<AbstractService>();

    // Assert
    instance1.Should().BeSameAs(instance2, because: "the implementation is a singleton (no transient markers)");
}
```

### Test 6: Disposable_implementation_of_abstract_class_is_transient

```csharp
[Fact]
public void Disposable_implementation_of_abstract_class_is_transient()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance1 = di.Resolve<DisposableAbstractService>();
    var instance2 = di.Resolve<DisposableAbstractService>();

    // Assert
    instance1.Should().NotBeSameAs(instance2, because: "IDisposable implementations should be transient");
}
```

### Test 7: Shares_singleton_implementation_across_abstract_class_resolutions

```csharp
[Fact]
public void Shares_singleton_implementation_across_abstract_class_resolutions()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var fromAbstract = di.Resolve<AbstractService>();
    var fromConcrete = di.Resolve<ConcreteService>();

    // Assert
    fromAbstract.Should().BeSameAs(fromConcrete, because: "the same singleton instance should be returned regardless of how it's requested");
}
```

---

## Implementation Steps

1. **Open file**: `/home/user/MagicDI/src/MagicDI.Tests/MagicDITests.InterfaceResolution.cs`

2. **Add helper classes**: Insert after line 211 (in `#region Test Interfaces and Classes`, before `#endregion`)

3. **Add test methods**: Insert after the existing `Resolves_abstract_class_to_implementation` test (around line 123)

4. **Build**:
   ```bash
   dotnet build src/MagicDI.sln -c Release
   ```

5. **Run tests**:
   ```bash
   dotnet test src/MagicDI.sln --filter "FullyQualifiedName~InterfaceResolution"
   ```

---

## Expected Results

After implementation, the test count for `InterfaceResolution` class will increase from 9 to 16 tests:

| Category | Before | After |
|----------|--------|-------|
| Interface tests | 8 | 8 |
| Abstract class tests | 1 | 8 |
| **Total** | **9** | **16** |

---

## Summary

This plan adds 7 new tests mirroring the existing interface tests:

| New Test | Mirrors |
|----------|---------|
| `Throws_when_no_abstract_implementation_exists` | `Throws_when_no_implementation_exists` |
| `Throws_when_multiple_implementations_of_abstract_class_exist` | `Throws_when_multiple_implementations_exist` |
| `Resolves_abstract_class_dependency_in_constructor` | `Resolves_interface_dependency_in_constructor` |
| `Resolves_nested_abstract_class_dependencies` | `Resolves_nested_interface_dependencies` |
| `Determines_lifetime_from_abstract_class_implementation` | `Determines_lifetime_from_implementation` |
| `Disposable_implementation_of_abstract_class_is_transient` | `Disposable_implementation_is_transient` |
| `Shares_singleton_implementation_across_abstract_class_resolutions` | `Shares_singleton_implementation_across_interface_resolutions` |
