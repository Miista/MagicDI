# MagicDI Library Extraction Plan

## Overview

This document outlines the plan for extracting the MagicDI dependency injection container from its current embedded location in the Sandbox project into a proper, standalone library project.

### Current State
- **Implementation Location**: `src/Sandbox/Program.cs` (lines 50-139)
- **Project Structure**: Single `Sandbox` console application
- **Build System**: Cake build expecting `./src/MagicDI/MagicDI.csproj` (does not exist)
- **Tests**: None (despite build.cake expecting them)
- **NuGet Spec**: Ready at `nuget/MagicDI.nupec` targeting `netstandard2.0`

### Target State
- Separate `MagicDI` class library project
- `Sandbox` project references the library for demonstration
- Test project with comprehensive coverage
- Working build, test, and pack pipeline

---

## Phase 1: Create Library Project Structure

### Step 1.1: Create MagicDI Library Project

Create a new class library project at `src/MagicDI/MagicDI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>

    <!-- Package metadata -->
    <PackageId>MagicDI</PackageId>
    <Version>0.1.0</Version>
    <Authors>Søren Guldmund</Authors>
    <Description>A lightweight, reflection-based dependency injection container for .NET with zero configuration.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Miista/MagicDI</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Miista/MagicDI</RepositoryUrl>
    <PackageTags>dependency-injection;di;ioc;container;reflection</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>

    <!-- Build options -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
</Project>
```

### Step 1.2: Create Directory Structure

```
src/MagicDI/
├── MagicDI.csproj
├── MagicDI.cs              # Main container class
├── Lifetime.cs             # Lifetime enum
├── InstanceRegistry.cs     # Internal instance tracking
└── Internals/              # (Optional) Internal helpers
```

---

## Phase 2: Extract and Refactor Code

### Step 2.1: Extract Lifetime Enum

Create `src/MagicDI/Lifetime.cs`:

```csharp
namespace MagicDI
{
    /// <summary>
    /// Specifies the lifetime of a resolved dependency.
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// A new instance is created for each scope.
        /// </summary>
        Scoped,

        /// <summary>
        /// A new instance is created every time the dependency is resolved.
        /// </summary>
        Transient,

        /// <summary>
        /// A single instance is shared across all resolutions.
        /// </summary>
        Singleton
    }
}
```

### Step 2.2: Extract InstanceRegistry Class

Create `src/MagicDI/InstanceRegistry.cs` (internal class):

```csharp
namespace MagicDI
{
    internal class InstanceRegistry
    {
        public object Instance { get; }
        public Lifetime Lifetime { get; }

        public InstanceRegistry(object instance, Lifetime lifetime)
        {
            Instance = instance;
            Lifetime = lifetime;
        }
    }
}
```

### Step 2.3: Extract Main MagicDI Class

Create `src/MagicDI/MagicDI.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MagicDI
{
    /// <summary>
    /// A lightweight dependency injection container that uses reflection
    /// to automatically resolve constructor dependencies.
    /// </summary>
    public class MagicDI
    {
        private readonly Dictionary<Type, InstanceRegistry> _instances =
            new Dictionary<Type, InstanceRegistry>();

        /// <summary>
        /// Resolves an instance of the specified type, automatically
        /// resolving all constructor dependencies.
        /// </summary>
        /// <typeparam name="T">The type to resolve.</typeparam>
        /// <returns>An instance of the specified type.</returns>
        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        private object Resolve(Type type)
        {
            if (_instances.TryGetValue(type, out var registry))
            {
                if (registry.Lifetime == Lifetime.Singleton)
                {
                    return registry.Instance;
                }
            }

            var instance = ResolveInstance(type);
            var lifetime = DetermineLifeTime(instance);
            _instances[type] = new InstanceRegistry(instance, lifetime);

            return instance;
        }

        private object ResolveInstance(Type type)
        {
            var constructor = GetConstructor(type);
            var arguments = ResolveConstructorArguments(constructor);
            return constructor.Invoke(arguments);
        }

        private ConstructorInfo GetConstructor(Type type)
        {
            var constructors = type.GetConstructors();
            return constructors
                .OrderByDescending(c => c.GetParameters().Length)
                .First();
        }

        private object[] ResolveConstructorArguments(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = Resolve(parameters[i].ParameterType);
            }

            return arguments;
        }

        private Lifetime DetermineLifeTime(object instance)
        {
            // Currently defaults to Singleton
            // Future: Support attribute-based or registration-based lifetime
            return Lifetime.Singleton;
        }
    }
}
```

---

## Phase 3: Update Existing Projects

### Step 3.1: Update Solution File

Add the new MagicDI project to `src/MagicDI.sln`:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MagicDI", "MagicDI\MagicDI.csproj", "{NEW-GUID}"
EndProject
```

### Step 3.2: Update Sandbox Project

1. Add project reference to MagicDI:

```xml
<ItemGroup>
  <ProjectReference Include="..\MagicDI\MagicDI.csproj" />
</ItemGroup>
```

2. Simplify `src/Sandbox/Program.cs`:

```csharp
using System;
using MagicDI;

namespace Sandbox
{
    // Demo service classes
    public class SomeService
    {
        private readonly SomeOtherService _someOtherService;

        public SomeService(SomeOtherService someOtherService)
        {
            _someOtherService = someOtherService;
        }
    }

    public class SomeOtherService { }

    class Program
    {
        static void Main(string[] args)
        {
            var di = new MagicDI.MagicDI();

            var service = di.Resolve<SomeService>();
            var service1 = di.Resolve<SomeService>();
            var service2 = di.Resolve<SomeService>();

            Console.WriteLine($"Same instance: {ReferenceEquals(service, service1)}");
            Console.WriteLine($"Same instance: {ReferenceEquals(service1, service2)}");
        }
    }
}
```

---

## Phase 4: Add Test Project

### Step 4.1: Create Test Project

Create `src/MagicDI.Tests/MagicDI.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MagicDI\MagicDI.csproj" />
  </ItemGroup>
</Project>
```

### Step 4.2: Add Unit Tests

Create `src/MagicDI.Tests/MagicDITests.cs`:

```csharp
using Xunit;

namespace MagicDI.Tests
{
    public class MagicDITests
    {
        [Fact]
        public void Resolve_SimpleType_ReturnsInstance()
        {
            var di = new MagicDI();
            var instance = di.Resolve<SimpleClass>();
            Assert.NotNull(instance);
        }

        [Fact]
        public void Resolve_TypeWithDependency_ResolvesDependency()
        {
            var di = new MagicDI();
            var instance = di.Resolve<ClassWithDependency>();
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency);
        }

        [Fact]
        public void Resolve_Singleton_ReturnsSameInstance()
        {
            var di = new MagicDI();
            var instance1 = di.Resolve<SimpleClass>();
            var instance2 = di.Resolve<SimpleClass>();
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Resolve_NestedDependencies_ResolvesAll()
        {
            var di = new MagicDI();
            var instance = di.Resolve<ClassWithNestedDependency>();
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency);
            Assert.NotNull(instance.Dependency.Dependency);
        }

        [Fact]
        public void Resolve_MultipleConstructorParameters_ResolvesAll()
        {
            var di = new MagicDI();
            var instance = di.Resolve<ClassWithMultipleDependencies>();
            Assert.NotNull(instance);
            Assert.NotNull(instance.Dependency1);
            Assert.NotNull(instance.Dependency2);
        }

        // Test classes
        public class SimpleClass { }

        public class ClassWithDependency
        {
            public SimpleClass Dependency { get; }
            public ClassWithDependency(SimpleClass dependency)
            {
                Dependency = dependency;
            }
        }

        public class ClassWithNestedDependency
        {
            public ClassWithDependency Dependency { get; }
            public ClassWithNestedDependency(ClassWithDependency dependency)
            {
                Dependency = dependency;
            }
        }

        public class ClassWithMultipleDependencies
        {
            public SimpleClass Dependency1 { get; }
            public ClassWithDependency Dependency2 { get; }

            public ClassWithMultipleDependencies(
                SimpleClass dependency1,
                ClassWithDependency dependency2)
            {
                Dependency1 = dependency1;
                Dependency2 = dependency2;
            }
        }
    }
}
```

### Step 4.3: Update Solution File

Add test project to solution.

---

## Phase 5: Update Build System

### Step 5.1: Verify build.cake Compatibility

The existing `build.cake` already expects the correct structure:
- Library at `./src/MagicDI/MagicDI.csproj`
- Test runner targeting `./src/**/*.Tests.csproj`
- Pack task targeting the library

No changes should be needed to `build.cake`.

### Step 5.2: Update NuGet Specification (Optional)

The `nuget/MagicDI.nupec` can be deprecated in favor of SDK-style project packaging. The project file now contains all NuGet metadata.

---

## Phase 6: Verification Checklist

### Build Verification
- [ ] `dotnet build src/MagicDI.sln` succeeds
- [ ] `./tools/dotnet-cake --target=Build` succeeds

### Test Verification
- [ ] `dotnet test src/MagicDI.Tests/MagicDI.Tests.csproj` passes all tests
- [ ] `./tools/dotnet-cake --target=Test` succeeds

### Pack Verification
- [ ] `dotnet pack src/MagicDI/MagicDI.csproj` creates NuGet package
- [ ] `./tools/dotnet-cake --target=Pack` succeeds
- [ ] Package contains correct DLL and documentation

### Runtime Verification
- [ ] `dotnet run --project src/Sandbox/Sandbox.csproj` executes successfully
- [ ] Output shows singleton behavior (same instance references)

---

## Implementation Order

1. **Create library project** (`src/MagicDI/MagicDI.csproj`)
2. **Extract Lifetime enum** (`src/MagicDI/Lifetime.cs`)
3. **Extract InstanceRegistry** (`src/MagicDI/InstanceRegistry.cs`)
4. **Extract MagicDI class** (`src/MagicDI/MagicDI.cs`)
5. **Update solution file** (add MagicDI project)
6. **Update Sandbox project** (add reference, simplify code)
7. **Create test project** (`src/MagicDI.Tests/`)
8. **Add unit tests**
9. **Update solution file** (add test project)
10. **Verify build pipeline**
11. **Verify test pipeline**
12. **Verify pack pipeline**

---

## Future Enhancements (Out of Scope)

These items are noted for future consideration but are not part of this extraction:

1. **Lifetime attribute support** - Allow `[Transient]`, `[Scoped]` attributes
2. **Interface-to-implementation registration** - `Register<IService, ServiceImpl>()`
3. **Scope support** - Create child scopes with `CreateScope()`
4. **Circular dependency detection** - Prevent infinite recursion
5. **Constructor selection attribute** - `[PreferredConstructor]`
6. **Factory delegates** - `Register<T>(Func<T> factory)`
7. **Lazy resolution** - `Resolve<Lazy<T>>()`

---

## Estimated Complexity

| Phase | Description | Complexity |
|-------|-------------|------------|
| 1 | Create library project | Low |
| 2 | Extract and refactor code | Low |
| 3 | Update existing projects | Low |
| 4 | Add test project | Medium |
| 5 | Update build system | Low |
| 6 | Verification | Low |

The extraction is straightforward as the code is already well-structured and isolated. The main work involves creating the proper project structure and ensuring the build pipeline works correctly.
