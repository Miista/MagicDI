# Implementation Plans

This directory contains implementation plans for MagicDI features and improvements.

## Recommended Implementation Order

### Phase 1 - Cleanup & Test Coverage (Low Risk)

| Order | Plan | Description | Complexity | Status |
|-------|------|-------------|------------|--------|
| 1 | [plan-01](plan-01-remove-scoped-enum.md) | Remove Scoped Enum | Low | Done |
| 2 | [plan-07](plan-07-additional-primitives.md) | Additional Primitives (test-only) | Low | Done |
| 3 | [plan-10](plan-10-container-isolation.md) | Container Isolation (test-only) | Low | Done |
| 4 | [plan-08](plan-08-exception-recovery.md) | Exception Recovery (test-only) | Low | Done |

### Phase 2 - Small Fixes

| Order | Plan | Description | Complexity | Status |
|-------|------|-------------|------------|--------|
| 5 | [plan-06](plan-06-lifetime-attributes.md) | Lifetime Attribute Inheritance | Low | Done |
| 6 | [plan-02](plan-02-value-types.md) | Value Types (better error messages) | Low |

### Phase 3 - Medium Improvements

| Order | Plan | Description | Complexity |
|-------|------|-------------|------------|
| 7 | [plan-05](plan-05-constructor-selection.md) | Constructor Selection Edge Cases | Medium |
| 8 | [plan-04](plan-04-implementation-finder.md) | Implementation Finder Edge Cases | Medium |
| 9 | [plan-09](plan-09-concurrency.md) | Concurrency Edge Cases | Medium |

### Phase 4 - Major Features

| Order | Plan | Description | Complexity |
|-------|------|-------------|------------|
| 10 | [plan-11](plan-11-ienumerable-resolution.md) | IEnumerable Resolution | High |
| 11 | [plan-12](plan-12-generic-resolution.md) | Generic Resolution | High |
| 12 | [plan-03](plan-03-generic-types.md) | Generic Types (deferred until plan-12) | Medium |
| 13 | [plan-13](plan-13-scoped-lifetime.md) | Scoped Lifetime | High |

## Rationale

- **Plan 01 first**: Removes unused `Scoped` enum before Plan 13 re-adds it properly
- **Phase 1 priorities**: Test-only changes validate existing behavior with zero risk
- **Plan 03 deferred**: Waits for Plan 12 (Generic Resolution) to leverage that infrastructure
- **Plan 13 last**: Scoped lifetime is the most invasive change (new interface, modified resolution flow, disposal tracking)
- **Plans 11 & 12**: Can be done in either order, but IEnumerable is slightly simpler since it builds on existing `ImplementationFinder` patterns

## Plan Summary

| Plan | Purpose |
|------|---------|
| 01 | Remove unused `Lifetime.Scoped` enum value |
| 02 | Reject structs/value types early with clear errors |
| 03 | Test/document generic type handling |
| 04 | Test assembly loading edge cases in ImplementationFinder |
| 05 | Handle static classes, ref/out params in constructor selection |
| 06 | Enable lifetime attribute inheritance (`Inherited = true`) |
| 07 | Expand primitive type test coverage |
| 08 | Test context stack cleanup on exceptions |
| 09 | Test thread safety under concurrent load |
| 10 | Verify singletons are isolated per container instance |
| 11 | Resolve `IEnumerable<T>` to all implementations |
| 12 | Resolve closed generic interfaces to open generic implementations |
| 13 | Add `CreateScope()` and scoped lifetime support |
