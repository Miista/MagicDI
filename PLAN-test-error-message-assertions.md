# Implementation Plan: Improve Error Message Assertions in Interface Resolution Tests

## Overview

The interface resolution tests in `MagicDITests.InterfaceResolution.cs` currently use loose wildcard assertions that do not verify all the important details in error messages. The implementation provides detailed error messages including:
- The interface/abstract type name being resolved
- For multiple implementations: the list of competing implementation type names
- Actionable guidance text

The current tests only verify a minimal substring, missing opportunities to catch regressions.

## Problem Analysis

### Current Test Code

**Test 1: No Implementation Exists (lines 56-67)**

```csharp
[Fact]
public void Throws_when_no_implementation_exists()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IUnimplementedInterface>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
        .WithMessage("*No implementation found*", because: "the error message should explain the problem");
}
```

**Test 2: Multiple Implementations Exist (lines 69-81)**

```csharp
[Fact]
public void Throws_when_multiple_implementations_exist()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IMultipleImplementations>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
        .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity");
}
```

### Actual Error Messages from Implementation

From `/home/user/MagicDI/src/MagicDI/ImplementationFinder.cs`:

**No Implementation (lines 61-63):**
```csharp
throw new InvalidOperationException(
    $"No implementation found for {interfaceType.FullName}. " +
    "Ensure a concrete class implementing this interface exists.");
```

**Multiple Implementations (lines 52-55):**
```csharp
var candidateNames = string.Join(", ", candidates.Select(c => c.FullName));
throw new InvalidOperationException(
    $"Multiple implementations found for {interfaceType.FullName}: {candidateNames}. " +
    "Cannot resolve ambiguous interface.");
```

### Pattern Inconsistency

The InterfaceResolution tests use single generic wildcard assertions, but the codebase shows a better pattern in other tests:

**CircularDependency tests** use multiple assertions:
```csharp
act.Should().Throw<InvalidOperationException>()
    .WithMessage("*IndirectCircularA*", because: "...");
```

**CaptiveDependency tests** use chained assertions:
```csharp
act.Should().Throw<InvalidOperationException>()
    .WithMessage("*SingletonWithTransientDep*")
    .WithMessage("*DisposableClass*", because: "...");
```

---

## Proposed Changes

### Change 1: Update `Throws_when_no_implementation_exists`

**Before:**
```csharp
act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
    .WithMessage("*No implementation found*", because: "the error message should explain the problem");
```

**After:**
```csharp
act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
    .WithMessage("*No implementation found*", because: "the error message should explain the problem")
    .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name")
    .WithMessage("*Ensure*concrete class*exists*", because: "the error message should provide guidance");
```

### Change 2: Add Dedicated Type Name Test

```csharp
[Fact]
public void No_implementation_exception_includes_interface_type_name()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IUnimplementedInterface>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name to help developers identify which interface lacks an implementation");
}
```

### Change 3: Update `Throws_when_multiple_implementations_exist`

**Before:**
```csharp
act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
    .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity");
```

**After:**
```csharp
act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
    .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity")
    .WithMessage("*IMultipleImplementations*", because: "the error message should include the interface type name")
    .WithMessage("*ambiguous*", because: "the error message should clarify this is an ambiguity issue");
```

### Change 4: Add Dedicated Competing Types Test

```csharp
[Fact]
public void Multiple_implementations_exception_lists_competing_types()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IMultipleImplementations>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*ImplementationA*", because: "the error message should list the first competing implementation")
        .WithMessage("*ImplementationB*", because: "the error message should list all competing implementations to help developers resolve the ambiguity");
}
```

---

## Complete Updated Test Code

Replace lines 55-81 in `MagicDITests.InterfaceResolution.cs`:

```csharp
[Fact]
public void Throws_when_no_implementation_exists()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IUnimplementedInterface>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "there is no implementation for the interface")
        .WithMessage("*No implementation found*", because: "the error message should explain the problem")
        .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name")
        .WithMessage("*Ensure*concrete class*exists*", because: "the error message should provide guidance");
}

[Fact]
public void No_implementation_exception_includes_interface_type_name()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IUnimplementedInterface>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*IUnimplementedInterface*", because: "the error message should include the interface type name to help developers identify which interface lacks an implementation");
}

[Fact]
public void Throws_when_multiple_implementations_exist()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IMultipleImplementations>();

    // Assert
    act.Should().Throw<InvalidOperationException>(because: "multiple implementations create ambiguity")
        .WithMessage("*Multiple implementations*", because: "the error message should explain the ambiguity")
        .WithMessage("*IMultipleImplementations*", because: "the error message should include the interface type name")
        .WithMessage("*ambiguous*", because: "the error message should clarify this is an ambiguity issue");
}

[Fact]
public void Multiple_implementations_exception_lists_competing_types()
{
    // Arrange
    var di = new MagicDI();

    // Act
    Action act = () => di.Resolve<IMultipleImplementations>();

    // Assert
    act.Should().Throw<InvalidOperationException>()
        .WithMessage("*ImplementationA*", because: "the error message should list the first competing implementation")
        .WithMessage("*ImplementationB*", because: "the error message should list all competing implementations to help developers resolve the ambiguity");
}
```

---

## Implementation Steps

1. **Open file**: `/home/user/MagicDI/src/MagicDI.Tests/MagicDITests.InterfaceResolution.cs`

2. **Update `Throws_when_no_implementation_exists`** (line 56): Add chained `.WithMessage()` calls

3. **Add `No_implementation_exception_includes_interface_type_name`**: Insert after the updated test

4. **Update `Throws_when_multiple_implementations_exist`** (line 69): Add chained `.WithMessage()` calls

5. **Add `Multiple_implementations_exception_lists_competing_types`**: Insert after the updated test

6. **Run tests**:
   ```bash
   dotnet test src/MagicDI.sln --filter "FullyQualifiedName~InterfaceResolution"
   ```

---

## Verification Checklist

- [ ] `Throws_when_no_implementation_exists` verifies "No implementation found"
- [ ] `Throws_when_no_implementation_exists` verifies interface type name
- [ ] `Throws_when_no_implementation_exists` verifies guidance text
- [ ] `No_implementation_exception_includes_interface_type_name` provides isolated type name check
- [ ] `Throws_when_multiple_implementations_exist` verifies "Multiple implementations"
- [ ] `Throws_when_multiple_implementations_exist` verifies interface type name
- [ ] `Throws_when_multiple_implementations_exist` verifies "ambiguous" keyword
- [ ] `Multiple_implementations_exception_lists_competing_types` verifies ImplementationA
- [ ] `Multiple_implementations_exception_lists_competing_types` verifies ImplementationB
- [ ] All tests pass

---

## Summary

| Aspect | Before | After |
|--------|--------|-------|
| Keyword verification | Generic only | Specific keywords |
| Type name verification | Missing | Present |
| Implementation names | Missing | Listed |
| Multiple assertions | Single call | Chained calls |
| Guidance verification | Missing | Verified |
