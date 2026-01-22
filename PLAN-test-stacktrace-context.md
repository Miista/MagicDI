# Implementation Plan: StackTrace-Based Calling Type Detection Tests

## Overview

### Problem Statement

The `GetCallingType()` method in `/home/user/MagicDI/src/MagicDI/MagicDI.cs` (lines 128-150) uses `StackTrace` to identify the calling type when `Resolve<T>()` is invoked. This calling type's assembly becomes the context for interface resolution via `ImplementationFinder.GetConcreteType()`. Currently, there are **no tests** verifying this critical behavior.

### Current Implementation

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
public T Resolve<T>()
{
    var callingType = GetCallingType();
    var resolved = Resolve(typeof(T), callingType);
    // ...
}

private static Type? GetCallingType()
{
    var stackTrace = new StackTrace();
    var frames = stackTrace.GetFrames();

    if (frames == null)
        return null;

    var magicDIAssembly = typeof(MagicDI).Assembly;

    foreach (var frame in frames)
    {
        var method = frame.GetMethod();
        var declaringType = method?.DeclaringType;

        if (declaringType != null && declaringType.Assembly != magicDIAssembly)
        {
            return declaringType;
        }
    }

    return null;
}
```

### Key Behaviors to Test

1. **Basic calling type detection**: The method correctly identifies the type that directly calls `Resolve<T>()`
2. **NoInlining guarantee**: The `[MethodImpl(MethodImplOptions.NoInlining)]` prevents JIT from optimizing away the call frame
3. **Assembly context propagation**: The detected calling type's assembly is used for interface resolution
4. **Edge cases**: Async methods, lambdas, nested calls, extension methods

### Testing Challenges

| Challenge | Impact | Mitigation Strategy |
|-----------|--------|---------------------|
| Stack frames can be optimized away in Release mode | False negatives in tests | Use `[MethodImpl(NoInlining)]` on test helper methods |
| Async state machines have generated types | Different `DeclaringType` in async methods | Test async scenarios explicitly |
| Lambda expressions create compiler-generated types | May return closure class instead | Verify compiler-generated type handling |
| Extension methods appear as static calls | Calling type is the extension method's class | Test extension method scenarios |

---

## Implementation Steps

### Step 1: Create Test File Structure

Create a new test file following existing patterns:

**File**: `src/MagicDI.Tests/MagicDITests.CallingTypeDetection.cs`

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class CallingTypeDetection
        {
            // Test scenarios go here
        }
    }
}
```

### Step 2: Add Internal Test Hook (Optional)

To directly verify `GetCallingType()` behavior, consider exposing an internal method for testing:

```csharp
// In MagicDI.cs
internal static Type? GetCallingTypeForTesting() => GetCallingType();

// Tests can call this via InternalsVisibleTo or reflection
```

### Step 3: Leverage Multi-Assembly Test Infrastructure

This plan depends on the multi-assembly infrastructure from `PLAN-test-context-aware-resolution.md`. The separate test assemblies (`MagicDI.Tests.AssemblyA`, `MagicDI.Tests.AssemblyB`) provide the ability to test that different calling assemblies result in different resolution contexts.

---

## Test Scenarios

### Category 1: Basic Calling Type Detection

#### Test 1.1: Direct call returns correct calling type
```csharp
[Fact]
public void Direct_call_detects_correct_calling_type()
{
    // Arrange
    var di = new MagicDI();

    // Act - call from this test class
    var instance = di.Resolve<SimpleService>();

    // Assert - verify the calling type context was this test class
    // (requires internal hook or observable side effect)
}
```

#### Test 1.2: Calling type is not MagicDI assembly
```csharp
[Fact]
public void Calling_type_is_never_from_MagicDI_assembly()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var result = ReflectionHelper.InvokeGetCallingType();

    // Assert
    result.Assembly.Should().NotBeSameAs(typeof(MagicDI).Assembly,
        because: "GetCallingType should skip MagicDI internal types");
}
```

### Category 2: Method Inlining Prevention

#### Test 2.1: NoInlining attribute is present
```csharp
[Fact]
public void Resolve_method_has_NoInlining_attribute()
{
    // Arrange
    var method = typeof(MagicDI).GetMethod("Resolve");

    // Act
    var attr = method.GetCustomAttribute<MethodImplAttribute>();

    // Assert
    attr.Should().NotBeNull(because: "Resolve must prevent inlining for StackTrace to work");
    attr.Value.Should().HaveFlag(MethodImplOptions.NoInlining);
}
```

#### Test 2.2: Resolve called through wrapper still works
```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
private T ResolveWrapper<T>(MagicDI di) => di.Resolve<T>();

[Fact]
public void Resolve_through_wrapper_detects_wrapper_as_caller()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = ResolveWrapper<SimpleService>(di);

    // Assert - calling type should be the test class (containing ResolveWrapper)
}
```

### Category 3: Async and Task-Based Calls

#### Test 3.1: Async method calling Resolve
```csharp
[Fact]
public async Task Async_caller_is_detected_correctly()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = await ResolveAsync<SimpleService>(di);

    // Assert - should detect this test class, not async state machine
}

[MethodImpl(MethodImplOptions.NoInlining)]
private async Task<T> ResolveAsync<T>(MagicDI di)
{
    await Task.Yield();
    return di.Resolve<T>();
}
```

#### Test 3.2: ConfigureAwait(false) scenarios
```csharp
[Fact]
public async Task Async_with_configure_await_false_still_works()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = await Task.Run(() => di.Resolve<SimpleService>()).ConfigureAwait(false);

    // Assert
    instance.Should().NotBeNull();
}
```

### Category 4: Lambda and Delegate Calls

#### Test 4.1: Lambda invoking Resolve
```csharp
[Fact]
public void Lambda_caller_returns_enclosing_type()
{
    // Arrange
    var di = new MagicDI();
    Func<SimpleService> resolver = () => di.Resolve<SimpleService>();

    // Act
    var instance = resolver();

    // Assert
    // Calling type will be compiler-generated closure class
    // This test documents the behavior
}
```

#### Test 4.2: Action delegate passed to another method
```csharp
[Fact]
public void Delegate_passed_to_other_method_detects_original_context()
{
    // Arrange
    var di = new MagicDI();
    SimpleService result = null;

    // Act
    ExecuteAction(() => result = di.Resolve<SimpleService>());

    // Assert
    result.Should().NotBeNull();
}

private void ExecuteAction(Action action) => action();
```

### Category 5: Nested and Indirect Calls

#### Test 5.1: Multiple levels of indirection
```csharp
[Fact]
public void Deep_call_stack_detects_first_non_MagicDI_type()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var instance = Level1(di);

    // Assert - calling type should be this test class via Level1
}

[MethodImpl(MethodImplOptions.NoInlining)]
private SimpleService Level1(MagicDI di) => Level2(di);

[MethodImpl(MethodImplOptions.NoInlining)]
private SimpleService Level2(MagicDI di) => Level3(di);

[MethodImpl(MethodImplOptions.NoInlining)]
private SimpleService Level3(MagicDI di) => di.Resolve<SimpleService>();
```

#### Test 5.2: Recursive resolution maintains correct context
```csharp
[Fact]
public void Nested_resolve_calls_maintain_context_stack()
{
    // Arrange
    var di = new MagicDI();

    // Act - resolve type with dependencies that also need resolution
    var instance = di.Resolve<ServiceWithDependencies>();

    // Assert - each dependency resolution used correct context
    instance.Dependency.Should().NotBeNull();
}
```

### Category 6: Multi-Assembly Context Resolution

These tests require the multi-assembly infrastructure and verify the integration between `GetCallingType()` and interface resolution.

#### Test 6.1: Same interface resolves differently based on caller assembly
```csharp
[Fact]
public void Interface_resolves_to_closest_assembly_implementation()
{
    // Arrange
    var di = new MagicDI();

    // Act
    var loggerA = ServiceA.ResolveLogger(di);  // From AssemblyA
    var loggerB = ServiceB.ResolveLogger(di);  // From AssemblyB

    // Assert
    loggerA.Should().BeOfType<ConsoleLogger>(
        because: "ServiceA calls from AssemblyA which contains ConsoleLogger");
    loggerB.Should().BeOfType<FileLogger>(
        because: "ServiceB calls from AssemblyB which contains FileLogger");
}
```

### Category 7: Edge Cases and Error Handling

#### Test 7.1: Null stack frames handling
```csharp
[Fact]
public void Handles_null_stack_frames_gracefully()
{
    // This is defensive - StackTrace.GetFrames() can theoretically return null
    // Test documents expected behavior when context cannot be determined
}
```

#### Test 7.2: Dynamic method invocation
```csharp
[Fact]
public void Dynamic_invoke_returns_reflection_type_as_caller()
{
    // Arrange
    var di = new MagicDI();
    var method = typeof(MagicDI).GetMethod("Resolve").MakeGenericMethod(typeof(SimpleService));

    // Act
    var instance = method.Invoke(di, null);

    // Assert
    // Documents behavior when called via reflection
}
```

### Category 8: Thread-Safety Under Concurrent Calls

#### Test 8.1: Concurrent resolves from different assemblies
```csharp
[Fact]
public async Task Concurrent_resolves_from_different_assemblies_are_isolated()
{
    // Arrange
    var di = new MagicDI();
    var barrier = new Barrier(2);

    // Act
    var taskA = Task.Run(() =>
    {
        barrier.SignalAndWait();
        return ServiceA.ResolveLogger(di);
    });

    var taskB = Task.Run(() =>
    {
        barrier.SignalAndWait();
        return ServiceB.ResolveLogger(di);
    });

    var results = await Task.WhenAll(taskA, taskB);

    // Assert - each task got the correct assembly-specific implementation
    results[0].Should().BeOfType<ConsoleLogger>();
    results[1].Should().BeOfType<FileLogger>();
}
```

---

## Relationship to Context-Aware Resolution Tests

The StackTrace-based calling type detection is the **foundation** for context-aware interface resolution. The relationship is:

```
┌─────────────────────┐
│  Resolve<T>()       │  ← Entry point with [NoInlining]
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  GetCallingType()   │  ← Uses StackTrace to find caller
└─────────┬───────────┘
          │ returns Type?
          ▼
┌─────────────────────┐
│  Resolve(Type,Type?)│  ← Internal method receives context
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  ImplementationFinder│  ← Uses context for "closest first" search
│  .GetConcreteType() │
└─────────────────────┘
```

### Test Organization

| Test File | Focus |
|-----------|-------|
| `MagicDITests.CallingTypeDetection.cs` | Tests `GetCallingType()` behavior directly |
| `MagicDITests.InterfaceResolution.cs` | Tests `ImplementationFinder` behavior (existing) |
| `MagicDITests.ContextAwareResolution.cs` | Integration tests combining both (multi-assembly) |

---

## Implementation Checklist

- [ ] Create `MagicDITests.CallingTypeDetection.cs` test file
- [ ] Add `InternalsVisibleTo` or internal test hook for `GetCallingType()` (optional)
- [ ] Implement Category 1 tests (basic detection)
- [ ] Implement Category 2 tests (NoInlining verification)
- [ ] Implement Category 3 tests (async scenarios)
- [ ] Implement Category 4 tests (lambda/delegate scenarios)
- [ ] Implement Category 5 tests (nested calls)
- [ ] Implement Category 6 tests (multi-assembly context) - depends on multi-assembly infrastructure
- [ ] Implement Category 7 tests (edge cases)
- [ ] Implement Category 8 tests (thread safety)
- [ ] Verify all tests pass in both Debug and Release configurations

---

## Risk Considerations

1. **JIT Optimization Sensitivity**: Tests may behave differently between Debug and Release builds. Run CI in both configurations.

2. **Framework Version Differences**: `StackTrace` behavior may vary between .NET versions. Test against multiple target frameworks if needed.

3. **Test Isolation**: Multi-assembly tests load assemblies that may persist across test runs. Use separate `MagicDI` instances per test.

4. **Async State Machine Types**: Compiler-generated types like `<ResolveAsync>d__0` have different names per compiler version. Avoid asserting on specific type names for async scenarios.
