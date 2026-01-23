# MagicDI

A lightweight, reflection-based dependency injection container for .NET that automatically resolves your dependencies like magic.

## Overview

MagicDI is a simple yet powerful dependency injection container that uses reflection to automatically discover and inject constructor dependencies. No complex configuration required - just resolve your types and let the magic happen.

## Features

- **Automatic Dependency Resolution**: Analyzes constructors and automatically resolves all dependencies recursively
- **Zero Configuration**: No need to register services manually - MagicDI figures it out
- **Interface & Abstract Class Resolution**: Automatically discovers and resolves interfaces and abstract classes to their implementations
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

## Interface and Abstract Class Resolution

MagicDI automatically resolves interfaces and abstract classes to their concrete implementations using a "closest first" strategy:

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

When resolving an interface, MagicDI searches for implementations using a "closest first" strategy based on both assembly and namespace proximity:

**Assembly order:**
1. **Requesting type's assembly** - The assembly containing the class that needs the dependency
2. **Referenced assemblies** - Assemblies directly referenced by the requesting assembly
3. **All loaded assemblies** - Fallback to scanning all assemblies in the AppDomain

**Namespace proximity (within an assembly):**
When multiple implementations exist in the same assembly, MagicDI picks the one with the closest namespace to the requesting type:
- Same namespace = distance 0
- Parent namespace = distance 1
- Sibling namespace = distance 2 (up one, down one)

```csharp
namespace MyApp.Services
{
    public class EmailService : IMessageService { }  // Distance 0 from Consumer
}

namespace MyApp.Providers
{
    public class SmsService : IMessageService { }    // Distance 2 from Consumer
}

namespace MyApp.Services
{
    public class Consumer(IMessageService service)   // Gets EmailService (closer)
    {
        public IMessageService Service { get; } = service;
    }
}
```

This also works across assemblies - each consumer gets the implementation "closest" to it:

```csharp
// In AssemblyA
public class ServiceA : ISharedService { }
public class ConsumerA(ISharedService service) { } // Gets ServiceA

// In AssemblyB
public class ServiceB : ISharedService { }
public class ConsumerB(ISharedService service) { } // Gets ServiceB
```

If no implementation exists, or if multiple implementations are at the same distance (making the choice truly ambiguous), an `InvalidOperationException` is thrown with a helpful error message.

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

MagicDI is intentionally minimal. If you need any of the following, use a full-featured container like Microsoft.Extensions.DependencyInjection, Autofac, or Ninject.

### No Constructor Parameter Configuration

Constructor parameters must be resolvable types. There's no mechanism to provide specific values:

```csharp
// This will throw - MagicDI has no way to know what values to use
public class ApiClient(string baseUrl, int timeout) { }

// Workaround: wrap configuration in a resolvable class
public class ApiConfig { public string BaseUrl { get; set; } public int Timeout { get; set; } }
public class ApiClient(ApiConfig config) { }
```

### No Factory Delegates

Cannot register factories or custom instantiation logic:

```csharp
// Not possible:
services.AddSingleton(sp => new MyService(
    sp.GetService<IDep>(),
    Environment.GetEnvironmentVariable("API_KEY")
));
```

### No Scoped Lifetime

Only Singleton and Transient are supported. There's no `CreateScope()`, no `IServiceScope`, no per-request lifetime:

```csharp
// Not supported:
services.AddScoped<DbContext>();
using var scope = provider.CreateScope();
```

### No Named or Keyed Services

Cannot have multiple implementations of the same interface distinguished by key:

```csharp
// Not possible - MagicDI picks implementations by namespace proximity, not by key
services.AddKeyedSingleton<ICache>("redis", new RedisCache());
services.AddKeyedSingleton<ICache>("memory", new MemoryCache());
```

### No Open Generic Resolution

MagicDI cannot automatically close open generic types. If you have `Repository<T> : IRepository<T>`, resolving `IRepository<User>` will fail because the assembly scan finds `Repository<>` (open), not `Repository<User>` (closed):

```csharp
public class Repository<T> : IRepository<T> { }

// This fails - MagicDI can't infer that Repository<User> implements IRepository<User>
var repo = di.Resolve<IRepository<User>>();

// Workaround: define explicit closed types
public class UserRepository : IRepository<User> { }
```

### No Lazy<T>, Func<T>, or IEnumerable<T>

Cannot inject deferred resolution, factories, or collections of implementations:

```csharp
// None of these work:
public class MyService(
    Lazy<IExpensive> lazy,           // Deferred resolution
    Func<IConnection> factory,        // Factory for new instances
    IEnumerable<IPlugin> plugins      // All implementations
) { }
```

### No Decorators or Interceptors

No AOP-style wrapping or decoration:

```csharp
// Not possible:
services.Decorate<IService, LoggingDecorator>();
services.AddInterceptor<CachingInterceptor>();
```

### No Disposal Tracking

The container does not track or dispose `IDisposable` instances. You're responsible for their lifecycle:

```csharp
var connection = di.Resolve<DbConnection>(); // Transient, implements IDisposable
// You must dispose this yourself - MagicDI won't

// Singleton IDisposables are also not tracked
// When your application shuts down, you need to handle cleanup manually
```

Implementing disposal tracking would require either scoped containers (where disposing the scope disposes all transients created within it) or a container-level `Dispose()` that tracks every transient created.

### Fixed Constructor Selection

Always selects the constructor with the most parameters. No way to specify which constructor to use:

```csharp
public class MyService
{
    public MyService() { }                    // Ignored
    public MyService(IDep dep) { }            // This one is always chosen

    // No [InjectionConstructor] or similar to override
}
```

### Ambiguous Implementations Fail

If multiple implementations exist at the same namespace distance, resolution throws rather than letting you choose:

```csharp
namespace MyApp.Services
{
    public class EmailNotifier : INotifier { }
    public class SmsNotifier : INotifier { }   // Same namespace = ambiguous
}
// Resolving INotifier throws InvalidOperationException
```

### When to Use MagicDI

MagicDI works well for:
- Simple applications with straightforward dependency graphs
- Prototypes and experiments where registration overhead isn't worth it
- Learning DI concepts without ceremony

Use a full-featured container if you need:
- Scoped lifetimes (web requests, unit of work)
- Open generic resolution (`IRepository<>` → `Repository<>`)
- Multiple keyed implementations of the same interface
- Container-managed disposal

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
