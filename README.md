# MagicDI

A lightweight, reflection-based dependency injection container for .NET that automatically resolves your dependencies like magic.

## Overview

MagicDI is a simple yet powerful dependency injection container that uses reflection to automatically discover and inject constructor dependencies. No complex configuration required - just resolve your types and let the magic happen.

## Features

- **Automatic Dependency Resolution**: Analyzes constructors and automatically resolves all dependencies recursively
- **Zero Configuration**: No need to register services manually - MagicDI figures it out
- **Interface Resolution**: Automatically discovers and resolves interfaces to their implementations
- **Lifetime Management**: Supports Singleton and Transient lifetimes with intelligent inference
- **Circular Dependency Detection**: Detects and reports circular dependencies with helpful error messages
- **Captive Dependency Validation**: Prevents common lifetime mismatch bugs
- **Type-Safe**: Generic `Resolve<T>()` method for compile-time type safety
- **Thread-Safe**: Safe for use in multi-threaded applications
- **Lightweight**: No external dependencies, uses only .NET standard library features
- **Broad Compatibility**: Targets .NET Standard 2.0 (.NET Framework 4.6.1+, .NET Core 2.0+)

## Installation

```bash
dotnet add package MagicDI
```

Or via NuGet Package Manager:

```
Install-Package MagicDI
```

## Quick Start

```csharp
using MagicDI;

// Define your services
public class DateProvider
{
    public DateTime Now => DateTime.Now;
}

public class SomeService
{
    private readonly DateProvider _dateProvider;

    // MagicDI will automatically inject DateProvider
    public SomeService(DateProvider dateProvider)
    {
        _dateProvider = dateProvider;
    }

    public void SomeMethod()
    {
        Console.WriteLine($"Current time: {_dateProvider.Now}");
    }
}

// Use the DI container
var di = new MagicDI();
var service = di.Resolve<SomeService>();
service.SomeMethod();
```

## Interface Resolution

MagicDI automatically resolves interfaces to their concrete implementations using a "closest first" strategy:

```csharp
public interface IMessageService
{
    void Send(string message);
}

public class EmailService : IMessageService
{
    public void Send(string message) => Console.WriteLine($"Email: {message}");
}

var di = new MagicDI();
var service = di.Resolve<IMessageService>(); // Returns EmailService instance
```

### Closest-First Resolution Strategy

When resolving an interface, MagicDI searches for implementations in this order:

1. **Requesting type's assembly** - The assembly containing the class that needs the dependency
2. **Referenced assemblies** - Assemblies directly referenced by the requesting assembly
3. **All loaded assemblies** - Fallback to scanning all assemblies in the AppDomain

This means multiple implementations of the same interface can coexist across different assemblies, and each consumer gets the implementation "closest" to it:

```csharp
// In AssemblyA
public class ServiceA : ISharedService { }
public class ConsumerA(ISharedService service) { } // Gets ServiceA

// In AssemblyB
public class ServiceB : ISharedService { }
public class ConsumerB(ISharedService service) { } // Gets ServiceB
```

If no implementation exists, or if multiple implementations are found within the same assembly (making the choice ambiguous), an `InvalidOperationException` is thrown with a helpful error message.

## How It Works

MagicDI uses reflection to:

1. **Discover Constructors**: Finds the constructor with the most parameters
2. **Analyze Dependencies**: Identifies all parameter types that need to be resolved
3. **Resolve Interfaces**: Automatically finds implementations for interface dependencies
4. **Recursive Resolution**: Resolves each dependency by recursively applying the same process
5. **Instance Management**: Caches instances according to their inferred or specified lifetime

## Lifetime Management

MagicDI supports two lifetime patterns with intelligent automatic inference:

### Singleton (Default)

The same instance is returned every time the type is resolved. This is the default for types that:
- Have no dependencies, or
- Have only singleton dependencies

```csharp
var di = new MagicDI();
var instance1 = di.Resolve<MyService>();
var instance2 = di.Resolve<MyService>();
// instance1 and instance2 are the same object
```

### Transient

A new instance is created every time the type is resolved. Types are automatically inferred as transient when:
- The type implements `IDisposable`
- Any of its dependencies are transient (lifetime cascades up)

```csharp
public class Connection : IDisposable
{
    public void Dispose() { /* cleanup */ }
}

var di = new MagicDI();
var conn1 = di.Resolve<Connection>();
var conn2 = di.Resolve<Connection>();
// conn1 and conn2 are different objects
```

### Explicit Lifetime with Attributes

You can explicitly specify a lifetime using the `[Lifetime]` attribute:

```csharp
[Lifetime(Lifetime.Transient)]
public class AlwaysNew
{
    // Always creates a new instance, even though it has no IDisposable
}

[Lifetime(Lifetime.Singleton)]
public class SharedResource : IDisposable
{
    // Singleton despite implementing IDisposable (use with caution)
    public void Dispose() { }
}
```

### Lifetime Inference Priority

MagicDI determines lifetime in this order:
1. Explicit `[Lifetime]` attribute (highest priority)
2. `IDisposable` implementation → Transient
3. Any transient dependency → Transient (cascades up)
4. Default → Singleton

### Captive Dependency Detection

MagicDI validates that explicit singletons don't depend on transient types, which would cause a "captive dependency" bug:

```csharp
[Lifetime(Lifetime.Singleton)]
public class BadService
{
    // This will throw InvalidOperationException!
    // A singleton holding a transient would "capture" it
    public BadService(IDisposable transientDep) { }
}
```

## Circular Dependency Detection

MagicDI detects circular dependencies and throws a helpful exception:

```csharp
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { } // Circular!
}

var di = new MagicDI();
di.Resolve<ServiceA>(); // Throws InvalidOperationException with the dependency chain
```

The error message includes the full resolution chain to help you identify and fix the cycle.

## Limitations

- **Primitive Types**: MagicDI cannot resolve primitive types (int, string, bool, etc.) as they require explicit values
- **Constructor Selection**: Always selects the constructor with the most parameters
- **Same-Assembly Ambiguity**: If multiple implementations of an interface exist in the same assembly, resolution fails (implementations across different assemblies work fine)
- **No Scoped Lifetime**: Scoped lifetime is not currently supported

## Building from Source

MagicDI uses [Cake](https://cakebuild.net/) for building:

```bash
# Build the solution
./tools/dotnet-cake --target=Build

# Run tests
./tools/dotnet-cake --target=Test

# Create NuGet package
./tools/dotnet-cake --target=Pack
```

Or using the dotnet CLI directly:

```bash
# Build
dotnet build src/MagicDI.sln -c Release

# Run tests
dotnet test src/MagicDI.sln
```

## Requirements

- .NET Standard 2.0 or higher
- .NET Framework 4.6.1 or higher
- .NET Core 2.0 or higher

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

**Søren Guldmund** ([@Miista](https://github.com/Miista))

## Repository

[https://github.com/Miista/MagicDI](https://github.com/Miista/MagicDI)

---

Made with magic and reflection
