# Plan 07: Additional Primitive Types

## Overview
Expand test coverage for all .NET primitive types.

## Current State

**InstanceFactory.cs (lines 33-35):**
```csharp
if (type.IsPrimitive)
    throw new InvalidOperationException(...);
```

**Current Test Coverage:** Only `int` is tested.

## .NET Primitives to Test

| Type | C# Keyword | Priority |
|------|------------|----------|
| `Byte` | `byte` | High |
| `SByte` | `sbyte` | Medium |
| `Int16` | `short` | High |
| `UInt16` | `ushort` | Medium |
| `Int32` | `int` | Already tested |
| `UInt32` | `uint` | Medium |
| `Int64` | `long` | High |
| `UInt64` | `ulong` | Medium |
| `Single` | `float` | High |
| `Double` | `double` | High |
| `Boolean` | `bool` | High |
| `Char` | `char` | High |
| `IntPtr` | `nint` | Low |
| `UIntPtr` | `nuint` | Low |

**Note:** `decimal` and `string` are NOT .NET primitives.

## Implementation

### Option 1: Theory with InlineData (Recommended)
```csharp
[Theory]
[InlineData(typeof(byte), "Byte")]
[InlineData(typeof(sbyte), "SByte")]
[InlineData(typeof(short), "Int16")]
// ... etc
public void Throws_when_resolving_primitive_type(Type primitiveType, string expectedTypeName)
{
    var di = new MagicDI();
    var resolveMethod = typeof(MagicDI).GetMethod(nameof(MagicDI.Resolve))!
        .MakeGenericMethod(primitiveType);

    Action act = () => resolveMethod.Invoke(di, null);

    act.Should().Throw<TargetInvocationException>()
        .WithInnerException<InvalidOperationException>()
        .WithMessage($"*{expectedTypeName}*primitive*");
}
```

### Option 2: Individual Tests
```csharp
[Fact] public void Throws_when_resolving_byte() => AssertPrimitiveThrows<byte>();
[Fact] public void Throws_when_resolving_bool() => AssertPrimitiveThrows<bool>();
// ... etc
```

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI.Tests/MagicDITests.cs` | Add primitive type tests to `ErrorHandling` |

## Estimated Effort
- Time: 15-30 minutes
- Complexity: Low
- Risk: Very low (test-only changes)
