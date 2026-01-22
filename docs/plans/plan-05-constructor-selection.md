# Plan 05: Constructor Selection Edge Cases

## Overview
Test and enhance constructor selection for edge cases including special types (merged from old Item 9).

## Current Analysis

`ConstructorSelector.cs`:
```csharp
var appropriateConstructor = type.GetConstructors()
    .OrderByDescending(info => info.GetParameters().Length)
    .ThenBy(info => info.MetadataToken)
    .FirstOrDefault();
```

- `GetConstructors()` returns only public instance constructors
- Selection: most parameters, MetadataToken for tiebreaking

## Edge Case Matrix

| Scenario | Current Behavior | Needs Fix |
|----------|------------------|-----------|
| Static class | Throws "no public constructors" | Yes - clearer error |
| Protected constructors | Correct - only public used | No (test only) |
| ref/out parameters | Fails confusingly | Yes |
| Default parameter values | Resolves all params | Decide behavior |
| C# record types | Works | No (test only) |
| Sealed classes | Works | No (test only) |
| Nested classes | Works | No (test only) |
| Tuple types | Not rejected | Yes |

## Implementation Steps

### Step 1: Add TypeValidator Helper
```csharp
internal static class TypeValidator
{
    public static void ValidateResolvable(Type type)
    {
        if (type.IsAbstract && type.IsSealed)  // Static class
            throw new InvalidOperationException($"Cannot resolve {type.Name} - static class");
        if (type.IsGenericTypeDefinition)
            throw new InvalidOperationException($"Cannot resolve {type.Name} - open generic");
    }
}
```

### Step 2: Enhance ConstructorSelector
Filter out constructors with ref/out parameters:
```csharp
var validConstructors = constructors
    .Where(c => !c.GetParameters().Any(p => p.ParameterType.IsByRef))
    .ToList();
```

### Step 3: Extend Value Type Checking
Add rejection for DateTime, Guid, TimeSpan, ValueTuple, enums.

## Test Classes to Create

### ConstructorSelectionEdgeCases
- `StaticClassHandling` - static class throws clear error
- `ProtectedInternalConstructors` - only public used
- `RefOutParameters` - ref/out rejected, fallback works
- `DefaultParameterValues` - document behavior
- `GenericTypeConstructors` - closed generics work

### SpecialTypeScenarios (merged from Item 9)
- `RecordTypes` - verify record constructor selection
- `SealedClasses` - should work normally
- `NestedClasses` - public resolvable, private fails
- `TupleTypes` - ValueTuple fails, Tuple<> fails differently
- `ValueTypes` - DateTime, Guid, etc. fail

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/MagicDI/TypeValidator.cs` | Create - centralized validation |
| `src/MagicDI/ConstructorSelector.cs` | Modify - ref/out filtering |
| `src/MagicDI/InstanceFactory.cs` | Modify - extend value type rejection |
| `src/MagicDI.Tests/MagicDITests.ConstructorSelectionEdgeCases.cs` | Create |
