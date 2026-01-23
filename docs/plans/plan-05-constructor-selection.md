# Plan 05: Constructor Selection Edge Cases

## Overview

Test and enhance constructor selection for edge cases.

## Analysis Summary

Most edge cases from the original plan were either:
- **Impossible at compile time** (static classes cannot be used as type arguments, ValueTuple as type arg)
- **Already working correctly** (records, sealed, nested, protected constructors)

The only real fix needed: **reject constructors with ref/out parameters**.

## Implementation

### Ref/Out Parameter Rejection

Modified `ConstructorSelector.GetConstructor()` to check for `IsByRef` parameters:

```csharp
var hasRefOut = constructors.Any(c =>
    c.GetParameters().Any(p => p.ParameterType.IsByRef));

if (hasRefOut)
    throw new InvalidOperationException(
        $"Cannot resolve instance of type {type.Name} because its constructor has ref or out parameters");
```

## Test Coverage

### RefOutParameterRejection
- `Throws_when_constructor_has_ref_parameter` - ref params rejected
- `Throws_when_constructor_has_out_parameter` - out params rejected
- `Throws_when_all_constructors_have_ref_or_out_parameters` - no fallback

### ExistingBehaviorDocumentation
- `Uses_only_public_constructors` - protected/internal/private ignored
- `Resolves_all_parameters_even_with_defaults` - defaults don't skip resolution
- `Resolves_record_types` - primary constructors work
- `Resolves_sealed_classes` - sealed works like any class
- `Resolves_public_nested_classes` - nesting is transparent

## Files Modified

| File | Change |
|------|--------|
| `src/MagicDI/ConstructorSelector.cs` | Added ref/out parameter rejection |
| `src/MagicDI.Tests/MagicDITests.ConstructorSelectionEdgeCases.cs` | Created test file |

## Status

**Complete**
