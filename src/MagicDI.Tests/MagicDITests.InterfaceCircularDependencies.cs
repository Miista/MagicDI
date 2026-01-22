using System;
using FluentAssertions;
using Xunit;

namespace MagicDI.Tests
{
    public partial class MagicDITests
    {
        public class InterfaceCircularDependency
        {
            #region Test Interfaces and Classes

            // Scenario 1: Direct Interface Circular
            public interface IServiceA { void DoWork(); }
            public interface IServiceB { void DoWork(); }

            public class ServiceA : IServiceA
            {
                public IServiceB ServiceB { get; }
                public ServiceA(IServiceB serviceB) { ServiceB = serviceB; }
                public void DoWork() { }
            }

            public class ServiceB : IServiceB
            {
                public IServiceA ServiceA { get; }
                public ServiceB(IServiceA serviceA) { ServiceA = serviceA; }
                public void DoWork() { }
            }

            // Scenario 2: Mixed Interface/Concrete Circular
            public interface IMixedService { void DoWork(); }

            public class MixedConcreteClass
            {
                public IMixedService Service { get; }
                public MixedConcreteClass(IMixedService service) { Service = service; }
            }

            public class MixedServiceImpl : IMixedService
            {
                public MixedConcreteClass Concrete { get; }
                public MixedServiceImpl(MixedConcreteClass concrete) { Concrete = concrete; }
                public void DoWork() { }
            }

            // Scenario 3: Self-Referencing Through Interface
            public interface ISelfReferencing { void DoSomething(); }

            public class SelfReferencingImpl : ISelfReferencing
            {
                public ISelfReferencing Self { get; }
                public SelfReferencingImpl(ISelfReferencing self) { Self = self; }
                public void DoSomething() { }
            }

            // Scenario 4: Three-Way Interface Circular
            public interface IAlpha { void Alpha(); }
            public interface IBeta { void Beta(); }
            public interface IGamma { void Gamma(); }

            public class AlphaImpl : IAlpha
            {
                public IBeta Beta { get; }
                public AlphaImpl(IBeta beta) { Beta = beta; }
                public void Alpha() { }
            }

            public class BetaImpl : IBeta
            {
                public IGamma Gamma { get; }
                public BetaImpl(IGamma gamma) { Gamma = gamma; }
                public void Beta() { }
            }

            public class GammaImpl : IGamma
            {
                public IAlpha Alpha { get; }
                public GammaImpl(IAlpha alpha) { Alpha = alpha; }
                public void Gamma() { }
            }

            // Scenario 5: Concrete Entry to Circular Interface Chain
            public interface IChainStart { void Start(); }
            public interface IChainEnd { void End(); }

            public class ConcreteEntry
            {
                public IChainStart ChainStart { get; }
                public ConcreteEntry(IChainStart chainStart) { ChainStart = chainStart; }
            }

            public class ChainStartImpl : IChainStart
            {
                public IChainEnd ChainEnd { get; }
                public ChainStartImpl(IChainEnd chainEnd) { ChainEnd = chainEnd; }
                public void Start() { }
            }

            public class ChainEndImpl : IChainEnd
            {
                public IChainStart ChainStart { get; }
                public ChainEndImpl(IChainStart chainStart) { ChainStart = chainStart; }
                public void End() { }
            }

            // Non-Circular Control
            public interface INonCircularService { void Work(); }
            public class NonCircularServiceImpl : INonCircularService { public void Work() { } }
            public class NonCircularConsumer
            {
                public INonCircularService Service { get; }
                public NonCircularConsumer(INonCircularService service) { Service = service; }
            }

            #endregion
        }
    }
}
