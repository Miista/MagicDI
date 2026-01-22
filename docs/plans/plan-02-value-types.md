# Plan 02: Value Types / Structs

## Overview
Add check in `MagicDI.Resolve<T>()` to reject value types with clear error messages.

## Current State Analysis

### Current Primitive Rejection
`InstanceFactory.cs` (lines 33-35):
```csharp
if (type.IsPrimitive)
    throw new InvalidOperationException(
        $"Cannot resolve instance of type {type.Name} because it is a primitive type");
```

**Critical Finding:** This check is effectively dead code. Value types fail earlier in `ImplementationFinder.GetConcreteType()` with confusing "No implementation found" errors.

### Type Behavior Matrix

| Type | `IsPrimitive` | `IsValueType` | Current Error |
|------|---------------|---------------|---------------|
| `int` | true | true | "No implementation found" |
| `DateTime` | false | true | "No implementation found" |
| `Guid` | false | true | "No implementation found" |
| `decimal` | false | true | "No implementation found" |
| Custom struct | false | true | "No implementation found" |

## Proposed Solution

### Recommendation: Reject All Value Types (`IsValueType`)
- Simple, predictable, follows DI conventions
- Major containers (Microsoft.Extensions.DI) only support class types

### Location of Check
Add in `MagicDI.Resolve()` private method, BEFORE `ImplementationFinder.GetConcreteType()`:

```csharp
private object Resolve(Type type, Type? requestingType)
{
    if (type.IsValueType)
    {
        throw new InvalidOperationException(
            $"Cannot resolve instance of type '{type.Name}' because it is a value type. " +
            $"MagicDI only supports resolving class types.");
    }
    // ... rest of method
}
```

## Test Scenarios

Add to `MagicDITests.General.ErrorHandling`:

```csharp
public class ValueTypes
{
    [Fact] Throws_when_resolving_DateTime()
    [Fact] Throws_when_resolving_Guid()
    [Fact] Throws_when_resolving_TimeSpan()
    [Fact] Throws_when_resolving_custom_struct()
    [Fact] Throws_when_resolving_enum()
    [Fact] Throws_when_resolving_decimal()
    [Fact] Throws_when_class_has_value_type_dependency()
}
```

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI/MagicDI.cs` | Add `IsValueType` check in `Resolve()` method |
| `src/MagicDI.Tests/MagicDITests.cs` | Add `ValueTypes` test class |

## Investigation Note
A simple `IsValueType` check may be too restrictive - there could be legitimate value types users want to resolve. Need to determine the right heuristic.
