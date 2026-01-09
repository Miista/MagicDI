# MagicDI

A lightweight, reflection-based dependency injection container for .NET that automatically resolves your dependencies like magic (

## Overview

MagicDI is a simple yet powerful dependency injection container that uses reflection to automatically discover and inject constructor dependencies. No complex configuration requiredjust resolve your types and let the magic happen.

## Features

- **Automatic Dependency Resolution**: Analyzes constructors and automatically resolves all dependencies recursively
- **Zero Configuration**: No need to register services manuallyMagicDI figures it out
- **Lifetime Management**: Supports Singleton, Transient, and Scoped lifetimes
- **Type-Safe**: Generic `Resolve<T>()` method for compile-time type safety
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

## How It Works

MagicDI uses reflection to:

1. **Discover Constructors**: Finds the constructor with the most parameters
2. **Analyze Dependencies**: Identifies all parameter types that need to be resolved
3. **Recursive Resolution**: Resolves each dependency by recursively applying the same process
4. **Instance Management**: Caches instances according to their configured lifetime

## Lifetime Management

MagicDI supports three lifetime patterns:

### Singleton
The same instance is returned every time the type is resolved:

```csharp
var di = new MagicDI();
var instance1 = di.Resolve<MyService>();
var instance2 = di.Resolve<MyService>();
// instance1 and instance2 are the same object
```

### Transient
A new instance is created every time the type is resolved:

```csharp
// Note: Currently under development
```

### Scoped
A new instance is created per resolution scope:

```csharp
// Note: Currently under development
```

## Limitations

- **Primitive Types**: MagicDI cannot resolve primitive types (int, string, bool, etc.) as they require explicit values
- **Circular Dependencies**: Circular dependencies will cause infinite recursion
- **Constructor Selection**: Always selects the constructor with the most parameters

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

Made with ( magic ( and reflection
