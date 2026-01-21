# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MagicDI is a lightweight, reflection-based Dependency Injection container for .NET. It targets .NET Standard 2.0 for broad compatibility and has zero external dependencies.

## Build Commands

```bash
# Build (using Cake - recommended)
./tools/dotnet-cake --target=Build

# Build (using dotnet CLI)
dotnet build src/MagicDI.sln -c Release

# Run tests
dotnet test src/MagicDI.sln

# Run a single test
dotnet test src/MagicDI.sln --filter "FullyQualifiedName~TestName"

# Create NuGet package
./tools/dotnet-cake --target=Pack

# Run sandbox demo
dotnet run --project src/Sandbox/Sandbox.csproj
```

## Architecture

The library consists of the following core files in `src/MagicDI/`:

- **MagicDI.cs**: Main container class with `Resolve<T>()` entry point. Manages singleton caching with thread-safe double-check locking and coordinates between lifetime resolution and instance creation.

- **LifetimeResolver.cs**: Internal class that determines type lifetimes using metadata analysis. Supports explicit `[Lifetime]` attributes, automatic `IDisposable` → Transient inference, and lifetime cascading from dependencies.

- **InstanceFactory.cs**: Internal class that creates instances by resolving constructor dependencies. Uses a resolver delegate to break circular class dependencies. Includes thread-local circular dependency detection.

- **ConstructorSelector.cs**: Static helper that selects the most appropriate constructor for a type.

- **Lifetime.cs**: Enum defining Singleton, Transient, and Scoped lifetimes.

- **LifetimeAttribute.cs**: Attribute for explicitly specifying a class's lifetime.

## Key Implementation Details

- **Constructor selection**: `ConstructorSelector.GetConstructor()` picks the constructor with the most parameters. Ties are broken by metadata token order.
- **Primitive rejection**: Throws `InvalidOperationException` for primitive types (int, string, bool, etc.) in `InstanceFactory.CreateInstance()`.
- **Circular dependency detection**: Both `LifetimeResolver` and `InstanceFactory` use thread-local stacks to detect circular dependencies during lifetime analysis and instance resolution.
- **Captive dependency validation**: Throws when a type marked `[Lifetime(Singleton)]` depends on a Transient type.
- **No interface registration**: Cannot map interfaces to implementations; only concrete types are supported.

## Test Structure

Tests are in `src/MagicDI.Tests/MagicDITests.cs` using xUnit. Test classes are defined inline for various scenarios (nested dependencies, multiple constructors, deep chains, etc.).
