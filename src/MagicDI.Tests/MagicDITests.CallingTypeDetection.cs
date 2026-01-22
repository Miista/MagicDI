using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class CallingTypeDetection
        {
            public class NoInliningVerification
            {
                [Fact]
                public void Resolve_method_has_NoInlining_flag()
                {
                    // Arrange
                    var method = typeof(MagicDI).GetMethod("Resolve");

                    // Act
                    var implFlags = method!.MethodImplementationFlags;

                    // Assert
                    implFlags.Should().HaveFlag(MethodImplAttributes.NoInlining,
                        because: "without NoInlining, JIT could optimize away the call frame and break GetCallingType");
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                private T ResolveWrapper<T>(MagicDI di) => di.Resolve<T>();

                [Fact]
                public void Resolve_through_wrapper_still_creates_instance()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = ResolveWrapper<SimpleService>(di);

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work even when called through a wrapper method");
                }
            }

            public class AsyncScenarios
            {
                [Fact]
                public async Task Async_caller_resolves_successfully()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = await ResolveAsync<SimpleService>(di);

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work from async methods");
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                private async Task<T> ResolveAsync<T>(MagicDI di)
                {
                    await Task.Yield();
                    return di.Resolve<T>();
                }

                [Fact]
                public async Task Async_from_task_run_still_works()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = await Task.Run(() => di.Resolve<SimpleService>());

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work regardless of synchronization context");
                }

            }

            public class LambdaAndDelegateScenarios
            {
                [Fact]
                public void Lambda_invoking_resolve_creates_instance()
                {
                    // Arrange
                    var di = new MagicDI();
                    Func<SimpleService> resolver = () => di.Resolve<SimpleService>();

                    // Act
                    var instance = resolver();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work when invoked from a lambda expression");
                }

                [Fact]
                public void Delegate_passed_to_other_method_resolves_correctly()
                {
                    // Arrange
                    var di = new MagicDI();
                    SimpleService result = null!;

                    // Act
                    ExecuteAction(() => result = di.Resolve<SimpleService>());

                    // Assert
                    result.Should().NotBeNull(
                        because: "resolution should work when the lambda is executed by another method");
                }

                private void ExecuteAction(Action action) => action();

                [Fact]
                public void Func_delegate_returns_resolved_instance()
                {
                    // Arrange
                    var di = new MagicDI();
                    Func<SimpleService> factory = () => di.Resolve<SimpleService>();

                    // Act
                    var result = InvokeFunc(factory);

                    // Assert
                    result.Should().NotBeNull(
                        because: "resolution should work when invoked through a Func delegate");
                }

                private T InvokeFunc<T>(Func<T> func) => func();

                [Fact]
                public void Captured_container_in_closure_works()
                {
                    // Arrange
                    var di = new MagicDI();
                    var captured = di;
                    Func<SimpleService> resolver = () => captured.Resolve<SimpleService>();

                    // Act
                    var instance = resolver();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work with captured variables in closures");
                }
            }

            public class NestedCallScenarios
            {
                [Fact]
                public void Multiple_levels_of_indirection_still_resolves()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = Level1(di);

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work through multiple levels of method calls");
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                private SimpleService Level1(MagicDI di) => Level2(di);

                [MethodImpl(MethodImplOptions.NoInlining)]
                private SimpleService Level2(MagicDI di) => Level3(di);

                [MethodImpl(MethodImplOptions.NoInlining)]
                private SimpleService Level3(MagicDI di) => di.Resolve<SimpleService>();
            }

            public class EdgeCases
            {
                [Fact]
                public void Dynamic_invoke_via_reflection_resolves_successfully()
                {
                    // Arrange
                    var di = new MagicDI();
                    var method = typeof(MagicDI).GetMethod("Resolve")!.MakeGenericMethod(typeof(SimpleService));

                    // Act
                    var instance = method.Invoke(di, null);

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work even when invoked via reflection");
                    instance.Should().BeOfType<SimpleService>();
                }

                [Fact]
                public void Generic_helper_method_resolves_correctly()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = GenericResolveHelper<SimpleService>(di);

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work through generic helper methods");
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                private T GenericResolveHelper<T>(MagicDI di) => di.Resolve<T>();

                [Fact]
                public void Extension_method_style_resolution_works()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.ResolveViaExtension<SimpleService>();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work when called through extension-style methods");
                }

                [Fact]
                public void Local_function_invoking_resolve_works()
                {
                    // Arrange
                    var di = new MagicDI();

                    SimpleService LocalResolve() => di.Resolve<SimpleService>();

                    // Act
                    var instance = LocalResolve();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "resolution should work when invoked from local functions");
                }
            }

            #region Test Classes

            public class SimpleService;

            public class ServiceWithDependency
            {
                public SimpleService Dependency { get; }

                public ServiceWithDependency(SimpleService dependency)
                {
                    Dependency = dependency;
                }
            }

            #endregion
        }
    }

    public static class MagicDIExtensions
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ResolveViaExtension<T>(this MagicDI di) => di.Resolve<T>();
    }
}
