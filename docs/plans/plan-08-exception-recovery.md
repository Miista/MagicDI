# Plan 08: Context Stack Cleanup on Exceptions

## Overview
Verify context stacks are properly cleaned up when exceptions occur.

## Current Context Stacks

| Location | Field | Type | Purpose |
|----------|-------|------|---------|
| MagicDI.cs:34 | `_contextStack` | `ThreadLocal<Stack<Type>>` | Requesting type context |
| LifetimeResolver.cs:26 | `_lifetimeStack` | `ThreadLocal<HashSet<Type>>` | Circular detection during lifetime |
| InstanceFactory.cs:20 | `_resolutionStack` | `ThreadLocal<HashSet<Type>>` | Circular detection during creation |

**All three use proper try/finally patterns for cleanup.**

## Exception Types

| Exception | Source | Condition |
|-----------|--------|-----------|
| `InvalidOperationException` | ConstructorSelector | No public constructors |
| `InvalidOperationException` | InstanceFactory | Primitive/circular dependency |
| `InvalidOperationException` | LifetimeResolver | Circular/captive dependency |
| `TargetInvocationException` | InstanceFactory | Constructor throws |

## Test Scenarios

### Scenario 1: Constructor Exception Recovery
```csharp
[Fact]
public void Container_remains_usable_after_constructor_throws()
{
    var di = new MagicDI();
    Action fail = () => di.Resolve<ClassWithThrowingDependency>();
    fail.Should().Throw<TargetInvocationException>();

    // Container still works
    var instance = di.Resolve<UnrelatedClass>();
    instance.Should().NotBeNull();
}
```

### Scenario 2: Deep Nesting (50+ Levels)
```csharp
[Fact]
public void Resolves_fifty_level_deep_dependency_chain()
{
    var di = new MagicDI();
    var instance = di.Resolve<DeepLevel50>();
    instance.Should().NotBeNull();
}
```

### Scenario 3: Recovery After Various Exceptions
- Captive dependency error recovery
- No implementation found recovery
- Multiple exceptions in sequence

## Test Class Structure

```
MagicDITests.ErrorRecovery.cs
├── ConstructorExceptionRecovery
│   ├── Container_remains_usable_after_constructor_throws
│   └── Can_resolve_sibling_dependencies_after_failure
├── CaptiveDependencyRecovery
│   └── Container_remains_usable_after_captive_error
├── DeepNesting
│   ├── Resolves_fifty_level_chain
│   └── Recovers_from_exception_at_depth
└── InterfaceResolutionRecovery
    └── Container_remains_usable_after_no_implementation
```

## Files to Create

| File | Purpose |
|------|---------|
| `src/MagicDI.Tests/MagicDITests.ErrorRecovery.cs` | New test file |

## Expected Outcome
All tests should pass with existing implementation (validates try/finally works).
