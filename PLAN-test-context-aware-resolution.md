# Implementation Plan: Context-Aware Resolution Tests for MagicDI

## 1. Overview

### Problem Statement

The `ImplementationFinder.GetAssembliesInSearchOrder()` method implements a "closest first" assembly search strategy:

1. **Requesting type's assembly** (highest priority)
2. **Referenced assemblies** (medium priority)
3. **All loaded assemblies** (fallback)

However, **all current tests exist in a single assembly** (`MagicDI.Tests`), making it impossible to verify that this assembly search order works correctly. The existing `InterfaceResolution` tests in `/home/user/MagicDI/src/MagicDI.Tests/MagicDITests.InterfaceResolution.cs` demonstrate basic interface resolution but cannot test cross-assembly resolution priority.

### The Feature Under Test

From `/home/user/MagicDI/src/MagicDI/ImplementationFinder.cs` (lines 66-107):

```csharp
private static IEnumerable<Assembly> GetAssembliesInSearchOrder(Type? requestingType)
{
    var visited = new HashSet<Assembly>();

    // 1. Start with requesting type's assembly (closest)
    if (requestingType != null)
    {
        var requestingAssembly = requestingType.Assembly;
        if (visited.Add(requestingAssembly))
        {
            yield return requestingAssembly;
        }

        // 2. Search referenced assemblies
        foreach (var referencedName in requestingAssembly.GetReferencedAssemblies())
        {
            // ... load and yield referenced assemblies
        }
    }

    // 3. Search all loaded assemblies (furthest fallback)
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        if (visited.Add(assembly))
        {
            yield return assembly;
        }
    }
}
```

### Proposed Solution

Create a multi-assembly test infrastructure with:

| Assembly | Purpose |
|----------|---------|
| `MagicDI.Tests.Contracts` | Shared interfaces |
| `MagicDI.Tests.AssemblyA` | Implementation A + Consumer A |
| `MagicDI.Tests.AssemblyB` | Implementation B + Consumer B |
| `MagicDI.Tests` | Test orchestration (references both A and B) |

---

## 2. Implementation Steps

### Step 2.1: Create Shared Contracts Assembly

**File:** `src/MagicDI.Tests.Contracts/MagicDI.Tests.Contracts.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

**File:** `src/MagicDI.Tests.Contracts/ISharedService.cs`

```csharp
namespace MagicDI.Tests.Contracts
{
    /// <summary>
    /// Interface for testing context-aware resolution.
    /// Different assemblies will provide different implementations.
    /// </summary>
    public interface ISharedService
    {
        string GetAssemblyName();
    }

    /// <summary>
    /// Second interface for testing multiple interfaces scenario.
    /// </summary>
    public interface IAnotherService
    {
        string GetOrigin();
    }
}
```

### Step 2.2: Create Assembly A

**File:** `src/MagicDI.Tests.AssemblyA/MagicDI.Tests.AssemblyA.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MagicDI\MagicDI.csproj" />
    <ProjectReference Include="..\MagicDI.Tests.Contracts\MagicDI.Tests.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**File:** `src/MagicDI.Tests.AssemblyA/SharedServiceA.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// Assembly A's implementation of ISharedService.
    /// </summary>
    public class SharedServiceA : ISharedService
    {
        public string GetAssemblyName() => "AssemblyA";
    }
}
```

**File:** `src/MagicDI.Tests.AssemblyA/ConsumerA.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// A consumer in Assembly A that depends on ISharedService.
    /// When resolved, should get SharedServiceA due to assembly proximity.
    /// </summary>
    public class ConsumerA
    {
        public ISharedService Service { get; }

        public ConsumerA(ISharedService service)
        {
            Service = service;
        }
    }
}
```

**File:** `src/MagicDI.Tests.AssemblyA/ResolverHelperA.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyA
{
    /// <summary>
    /// Helper class to resolve ISharedService from Assembly A's context.
    /// The call to Resolve<T>() happens within this assembly, so MagicDI
    /// uses this assembly as the requesting context.
    /// </summary>
    public static class ResolverHelperA
    {
        public static ISharedService ResolveSharedService(MagicDI di)
        {
            // This call originates from AssemblyA, so AssemblyA is searched first
            return di.Resolve<ISharedService>();
        }

        public static ConsumerA ResolveConsumer(MagicDI di)
        {
            return di.Resolve<ConsumerA>();
        }
    }
}
```

### Step 2.3: Create Assembly B

**File:** `src/MagicDI.Tests.AssemblyB/MagicDI.Tests.AssemblyB.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MagicDI\MagicDI.csproj" />
    <ProjectReference Include="..\MagicDI.Tests.Contracts\MagicDI.Tests.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**File:** `src/MagicDI.Tests.AssemblyB/SharedServiceB.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// Assembly B's implementation of ISharedService.
    /// </summary>
    public class SharedServiceB : ISharedService
    {
        public string GetAssemblyName() => "AssemblyB";
    }
}
```

**File:** `src/MagicDI.Tests.AssemblyB/ConsumerB.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// A consumer in Assembly B that depends on ISharedService.
    /// When resolved, should get SharedServiceB due to assembly proximity.
    /// </summary>
    public class ConsumerB
    {
        public ISharedService Service { get; }

        public ConsumerB(ISharedService service)
        {
            Service = service;
        }
    }
}
```

**File:** `src/MagicDI.Tests.AssemblyB/ResolverHelperB.cs`

```csharp
using MagicDI.Tests.Contracts;

namespace MagicDI.Tests.AssemblyB
{
    /// <summary>
    /// Helper class to resolve ISharedService from Assembly B's context.
    /// </summary>
    public static class ResolverHelperB
    {
        public static ISharedService ResolveSharedService(MagicDI di)
        {
            // This call originates from AssemblyB, so AssemblyB is searched first
            return di.Resolve<ISharedService>();
        }

        public static ConsumerB ResolveConsumer(MagicDI di)
        {
            return di.Resolve<ConsumerB>();
        }
    }
}
```

### Step 2.4: Update Solution File

**File:** `src/MagicDI.sln`

Add the three new projects to the solution.

### Step 2.5: Update Main Test Project

**File:** `src/MagicDI.Tests/MagicDI.Tests.csproj`

Add references to the new assemblies:

```xml
<ItemGroup>
  <ProjectReference Include="..\MagicDI\MagicDI.csproj" />
  <ProjectReference Include="..\MagicDI.Tests.Contracts\MagicDI.Tests.Contracts.csproj" />
  <ProjectReference Include="..\MagicDI.Tests.AssemblyA\MagicDI.Tests.AssemblyA.csproj" />
  <ProjectReference Include="..\MagicDI.Tests.AssemblyB\MagicDI.Tests.AssemblyB.csproj" />
</ItemGroup>
```

### Step 2.6: Create Test File

**File:** `src/MagicDI.Tests/MagicDITests.ContextAwareResolution.cs`

```csharp
using System;
using FluentAssertions;
using MagicDI.Tests.AssemblyA;
using MagicDI.Tests.AssemblyB;
using MagicDI.Tests.Contracts;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class ContextAwareResolution
        {
            public class DirectResolution
            {
                [Fact]
                public void Resolves_from_assembly_A_when_called_from_assembly_A()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ResolverHelperA.ResolveSharedService calls di.Resolve<ISharedService>() from AssemblyA
                    var service = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    service.GetAssemblyName().Should().Be("AssemblyA",
                        because: "the Resolve call originated from AssemblyA, so its implementation should be found first");
                }

                [Fact]
                public void Resolves_from_assembly_B_when_called_from_assembly_B()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - ResolverHelperB.ResolveSharedService calls di.Resolve<ISharedService>() from AssemblyB
                    var service = ResolverHelperB.ResolveSharedService(di);

                    // Assert
                    service.GetAssemblyName().Should().Be("AssemblyB",
                        because: "the Resolve call originated from AssemblyB, so its implementation should be found first");
                }
            }

            public class NestedDependencyResolution
            {
                [Fact]
                public void Consumer_in_assembly_A_gets_implementation_from_assembly_A()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerA>();

                    // Assert
                    consumer.Service.GetAssemblyName().Should().Be("AssemblyA",
                        because: "ConsumerA is in AssemblyA, so its ISharedService dependency should resolve to SharedServiceA");
                }

                [Fact]
                public void Consumer_in_assembly_B_gets_implementation_from_assembly_B()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerB>();

                    // Assert
                    consumer.Service.GetAssemblyName().Should().Be("AssemblyB",
                        because: "ConsumerB is in AssemblyB, so its ISharedService dependency should resolve to SharedServiceB");
                }
            }

            public class SameContainerDifferentContexts
            {
                [Fact]
                public void Same_container_resolves_different_implementations_based_on_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumerA = di.Resolve<ConsumerA>();
                    var consumerB = di.Resolve<ConsumerB>();

                    // Assert
                    consumerA.Service.Should().NotBeSameAs(consumerB.Service,
                        because: "each consumer gets its own assembly's implementation");

                    consumerA.Service.GetAssemblyName().Should().Be("AssemblyA");
                    consumerB.Service.GetAssemblyName().Should().Be("AssemblyB");
                }

                [Fact]
                public void Context_switches_correctly_between_resolve_calls()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act - Alternate between contexts
                    var serviceA1 = ResolverHelperA.ResolveSharedService(di);
                    var serviceB1 = ResolverHelperB.ResolveSharedService(di);
                    var serviceA2 = ResolverHelperA.ResolveSharedService(di);
                    var serviceB2 = ResolverHelperB.ResolveSharedService(di);

                    // Assert
                    serviceA1.GetAssemblyName().Should().Be("AssemblyA");
                    serviceB1.GetAssemblyName().Should().Be("AssemblyB");
                    serviceA2.GetAssemblyName().Should().Be("AssemblyA");
                    serviceB2.GetAssemblyName().Should().Be("AssemblyB");
                }
            }

            public class SingletonBehaviorWithContext
            {
                [Fact]
                public void Singleton_implementation_is_shared_within_same_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var service1 = ResolverHelperA.ResolveSharedService(di);
                    var service2 = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    service1.Should().BeSameAs(service2,
                        because: "singleton instances should be cached and reused within the same context");
                }

                [Fact]
                public void Consumer_and_direct_resolution_share_singleton_in_same_context()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var consumer = di.Resolve<ConsumerA>();
                    var directService = ResolverHelperA.ResolveSharedService(di);

                    // Assert
                    consumer.Service.Should().BeSameAs(directService,
                        because: "the same concrete type resolved in the same context should be the same singleton");
                }
            }
        }
    }
}
```

---

## 3. Test Scenarios Summary

| Category | Test Name | Purpose |
|----------|-----------|---------|
| **Direct Resolution** | `Resolves_from_assembly_A_when_called_from_assembly_A` | Verify calling context determines implementation |
| **Direct Resolution** | `Resolves_from_assembly_B_when_called_from_assembly_B` | Verify calling context determines implementation |
| **Nested Dependencies** | `Consumer_in_assembly_A_gets_implementation_from_assembly_A` | Verify context propagates through dependency chain |
| **Nested Dependencies** | `Consumer_in_assembly_B_gets_implementation_from_assembly_B` | Verify context propagates through dependency chain |
| **Multiple Contexts** | `Same_container_resolves_different_implementations_based_on_context` | Verify container handles multiple contexts correctly |
| **Multiple Contexts** | `Context_switches_correctly_between_resolve_calls` | Verify context isolation between calls |
| **Singleton Behavior** | `Singleton_implementation_is_shared_within_same_context` | Verify singleton caching works per-implementation |
| **Singleton Behavior** | `Consumer_and_direct_resolution_share_singleton_in_same_context` | Verify singleton sharing across resolution paths |

---

## 4. File Summary

### New Files to Create

| Path | Description |
|------|-------------|
| `src/MagicDI.Tests.Contracts/MagicDI.Tests.Contracts.csproj` | Shared contracts project |
| `src/MagicDI.Tests.Contracts/ISharedService.cs` | Interface definitions |
| `src/MagicDI.Tests.AssemblyA/MagicDI.Tests.AssemblyA.csproj` | Assembly A project |
| `src/MagicDI.Tests.AssemblyA/SharedServiceA.cs` | Implementation A |
| `src/MagicDI.Tests.AssemblyA/ConsumerA.cs` | Consumer A |
| `src/MagicDI.Tests.AssemblyA/ResolverHelperA.cs` | Resolution helper for A context |
| `src/MagicDI.Tests.AssemblyB/MagicDI.Tests.AssemblyB.csproj` | Assembly B project |
| `src/MagicDI.Tests.AssemblyB/SharedServiceB.cs` | Implementation B |
| `src/MagicDI.Tests.AssemblyB/ConsumerB.cs` | Consumer B |
| `src/MagicDI.Tests.AssemblyB/ResolverHelperB.cs` | Resolution helper for B context |
| `src/MagicDI.Tests/MagicDITests.ContextAwareResolution.cs` | Test file |

### Files to Modify

| Path | Change |
|------|--------|
| `src/MagicDI.sln` | Add three new projects |
| `src/MagicDI.Tests/MagicDI.Tests.csproj` | Add references to new projects |

---

## 5. Verification Steps

After implementation, run:

```bash
# Build all projects
dotnet build src/MagicDI.sln -c Release

# Run only the new tests
dotnet test src/MagicDI.sln --filter "FullyQualifiedName~ContextAwareResolution"

# Run all tests to ensure no regressions
dotnet test src/MagicDI.sln
```

---

## 6. Key Design Decisions

### Why Separate Assemblies?

The `GetCallingType()` method in `/home/user/MagicDI/src/MagicDI/MagicDI.cs` uses stack trace analysis to determine the calling context. For the context-aware resolution to work, the `Resolve<T>()` call must originate from a different assembly. This is why we need `ResolverHelperA` and `ResolverHelperB` - they provide methods that call `Resolve<T>()` from within their respective assemblies.

### Why ResolverHelper Classes?

When you call `di.Resolve<ISharedService>()` from the test project (`MagicDI.Tests`), the calling context is `MagicDI.Tests`, not `AssemblyA` or `AssemblyB`. The helper classes ensure the resolution originates from the correct assembly context.

### Singleton Caching Behavior

Based on the code in `/home/user/MagicDI/src/MagicDI/MagicDI.cs`, singletons are cached by concrete type. This means:
- `SharedServiceA` resolved from context A is cached as one singleton
- `SharedServiceB` resolved from context B is cached as a different singleton
- Both coexist in the same container
