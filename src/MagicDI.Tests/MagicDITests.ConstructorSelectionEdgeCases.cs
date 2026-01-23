using System;
using FluentAssertions;
using Xunit;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedParameter.Local

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class ConstructorSelectionEdgeCases
        {
            public class RefOutParameterRejection
            {
                [Fact]
                public void Throws_when_constructor_has_ref_parameter()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithRefParameter>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*ref or out parameters*",
                            because: "constructors with ref parameters cannot be invoked by DI");
                }

                [Fact]
                public void Throws_when_constructor_has_out_parameter()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithOutParameter>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*ref or out parameters*",
                            because: "constructors with out parameters cannot be invoked by DI");
                }

                [Fact]
                public void Throws_when_all_constructors_have_ref_or_out_parameters()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    Action act = () => di.Resolve<ClassWithAllRefOutConstructors>();

                    // Assert
                    act.Should().Throw<InvalidOperationException>()
                        .WithMessage("*ref or out parameters*",
                            because: "when all constructors have ref/out params, resolution should fail");
                }

                #region Test Classes

                public class ClassWithRefParameter
                {
                    public ClassWithRefParameter(ref int value)
                    {
                    }
                }

                public class ClassWithOutParameter
                {
                    public ClassWithOutParameter(out int value)
                    {
                        value = 0;
                    }
                }

                public class ClassWithAllRefOutConstructors
                {
                    public ClassWithAllRefOutConstructors(ref int value)
                    {
                    }

                    public ClassWithAllRefOutConstructors(out string text)
                    {
                        text = "";
                    }
                }

                #endregion
            }

            public class ExistingBehaviorDocumentation
            {
                [Fact]
                public void Uses_only_public_constructors()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithMixedAccessConstructors>();

                    // Assert
                    instance.Should().NotBeNull();
                    instance.UsedPublicConstructor.Should().BeTrue(
                        because: "only public constructors should be considered for resolution");
                }

                [Fact]
                public void Resolves_all_parameters_even_with_defaults()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<ClassWithDefaultParameters>();

                    // Assert
                    instance.Should().NotBeNull();
                    instance.Dependency.Should().NotBeNull(
                        because: "DI should resolve all parameters regardless of defaults");
                }

                [Fact]
                public void Resolves_record_types()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<RecordWithDependency>();

                    // Assert
                    instance.Should().NotBeNull();
                    instance.Dependency.Should().NotBeNull(
                        because: "records use primary constructor which should be resolved");
                }

                [Fact]
                public void Resolves_sealed_classes()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<SealedClassWithDependency>();

                    // Assert
                    instance.Should().NotBeNull();
                    instance.Dependency.Should().NotBeNull(
                        because: "sealed classes should resolve like any other class");
                }

                [Fact]
                public void Resolves_public_nested_classes()
                {
                    // Arrange
                    var di = new MagicDI();

                    // Act
                    var instance = di.Resolve<OuterClass.PublicNestedClass>();

                    // Assert
                    instance.Should().NotBeNull(
                        because: "public nested classes should be resolvable");
                }

                #region Test Classes

                public class ClassWithMixedAccessConstructors
                {
                    public bool UsedPublicConstructor { get; }

                    public ClassWithMixedAccessConstructors()
                    {
                        UsedPublicConstructor = true;
                    }

                    protected ClassWithMixedAccessConstructors(int ignored)
                    {
                        UsedPublicConstructor = false;
                    }

                    internal ClassWithMixedAccessConstructors(string ignored)
                    {
                        UsedPublicConstructor = false;
                    }

                    private ClassWithMixedAccessConstructors(bool ignored)
                    {
                        UsedPublicConstructor = false;
                    }
                }

                public class SimpleDependency;

                public class ClassWithDefaultParameters
                {
                    public SimpleDependency Dependency { get; }

                    public ClassWithDefaultParameters(SimpleDependency dependency = null)
                    {
                        Dependency = dependency;
                    }
                }

                public record RecordWithDependency(SimpleDependency Dependency);

                public sealed class SealedClassWithDependency
                {
                    public SimpleDependency Dependency { get; }

                    public SealedClassWithDependency(SimpleDependency dependency)
                    {
                        Dependency = dependency;
                    }
                }

                public class OuterClass
                {
                    public class PublicNestedClass;
                }

                #endregion
            }
        }
    }
}
