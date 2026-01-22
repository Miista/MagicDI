# Plan 01: Remove Scoped Enum Value

## Overview
Remove the unused `Lifetime.Scoped` enum value from `Lifetime.cs`.

## Current State Analysis

The `Lifetime.Scoped` enum value is defined in `/home/user/MagicDI/src/MagicDI/Lifetime.cs` at line 11:

```csharp
public enum Lifetime
{
    Scoped,    // Line 11 - UNUSED
    Transient,
    Singleton
}
```

### Usage Analysis
- **Code References:** Only the enum definition itself
- **No usages in:** `LifetimeResolver.cs`, `MagicDI.cs`, or any tests

### Documentation References (need updating)
1. `README.md:13` - Feature list mentions Scoped
2. `README.md:93-98` - Scoped section with "Currently under development"
3. `CLAUDE.md:43` - Architecture section mentions Scoped

## Files to Modify

| File | Change |
|------|--------|
| `src/MagicDI/Lifetime.cs` | Remove `Scoped` enum value and XML doc |
| `README.md` | Remove Scoped from feature list and section |
| `CLAUDE.md` | Update Lifetime.cs description |

## Step-by-Step Implementation

### Step 1: Modify Lifetime.cs
Remove lines 8-11 (the Scoped enum value and its XML doc comment).

### Step 2: Update README.md
1. Line 13 - Change to "Supports Singleton and Transient lifetimes"
2. Lines 93-98 - Remove the entire Scoped section

### Step 3: Update CLAUDE.md
Line 43 - Change to "Enum defining Singleton and Transient lifetimes"

### Step 4: Build and Test
```bash
dotnet build src/MagicDI.sln -c Release
dotnet test src/MagicDI.sln
```

## Risks and Considerations

**Low Risk: Binary Compatibility**
- Current: `Scoped = 0`, `Transient = 1`, `Singleton = 2`
- After: `Transient = 0`, `Singleton = 1`
- Since Scoped was never functional, this is acceptable

**Very Low Risk: External Code**
- README documents Scoped as "under development"
- No working code should reference it
