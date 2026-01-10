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

The library consists of three core files in `src/MagicDI/`:

- **MagicDI.cs**: Main container class with `Resolve<T>()` entry point. Uses a `Dictionary<Type, InstanceRegistry>` for caching. Resolution works by finding the constructor with most parameters, recursively resolving all parameter types, then invoking the constructor via reflection.

- **InstanceRegistry.cs**: Internal data holder tracking resolved types, their lifetime, and cached instance values.

- **Lifetime.cs**: Enum defining Singleton, Transient, and Scoped lifetimes. Note: Currently only Singleton is implemented (all resolved instances are cached and reused).

## Key Implementation Details

- **Constructor selection**: `GetConstructor()` picks the constructor with the most parameters. Ties are broken by metadata token order.
- **Primitive rejection**: Throws `InvalidOperationException` for primitive types (int, string, bool, etc.) in `ResolveInstance()`.
- **No circular dependency protection**: Circular dependencies cause `StackOverflowException` (test exists but is skipped).
- **No interface registration**: Cannot map interfaces to implementations; only concrete types are supported.

## Test Structure

Tests are in `src/MagicDI.Tests/MagicDITests.cs` using xUnit. Test classes are defined inline for various scenarios (nested dependencies, multiple constructors, deep chains, etc.).
