# Plan 04: Assembly/Implementation Discovery Edge Cases

## Overview
Test edge cases in `ImplementationFinder.cs` for assembly loading and error handling.

## Current Error Handling

### 1. Referenced Assembly Load Failures (Lines 82-96)
```csharp
try { referencedAssembly = Assembly.Load(referencedName); }
catch { /* Skip assemblies that can't be loaded */ }
```

### 2. ReflectionTypeLoadException Handling (Lines 118-122)
```csharp
catch (ReflectionTypeLoadException ex)
{
    types = ex.Types.Where(t => t != null).ToArray();
}
```

### 3. General Assembly Inspection Failures (Lines 123-127)
```csharp
catch { return candidates; }
```

## Test Scenarios

### Scenario 1: ReflectionTypeLoadException Partial Recovery
Verify container finds implementations from types that loaded successfully when some types fail.

### Scenario 2: Assembly Load Failures
Verify resolution continues when `Assembly.Load()` fails for referenced assemblies.

### Scenario 3: Transitive Assembly Resolution
Test: Test Assembly -> AssemblyA -> AssemblyC (contains implementation)

### Scenario 4: Diamond Dependency Pattern
```
       Tests
      /     \
     A       B
      \     /
        C (implementation)
```

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/MagicDI.Tests/MagicDITests.ImplementationFinder.cs` | New test file |
| `src/MagicDI.Tests.AssemblyC/` | New project for transitive tests |
| `src/MagicDI.sln` | Add AssemblyC project |

## Implementation Steps

1. Create test file structure with nested classes:
   - `AssemblySearchOrder`
   - `TransitiveDependencies`
   - `DiamondDependencies`
   - `ErrorRecovery`

2. Create AssemblyC project for transitive dependency testing

3. Update AssemblyA to reference AssemblyC

4. Implement tests for each scenario
