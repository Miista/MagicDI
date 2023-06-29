using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var di = new MagicDI();
            var service = di.Resolve<SomeService>();
            Console.WriteLine(service.SomeMethod());
            Thread.Sleep(1000);
            var service1 = di.Resolve<SomeService>();
            Console.WriteLine(service1.SomeMethod());
            Thread.Sleep(1000);
            var service2 = di.Resolve<SomeService>();
            Console.WriteLine(service2.SomeMethod());
        }
    }

    class SomeService
    {
        private readonly DateProvider _time;

        public SomeService(DateProvider time)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
        }

        public bool SomeMethod()
        {
            Console.WriteLine(_time.Get());
            return true;
        }
    }

    class DateProvider
    {
        private readonly DateTime _now = DateTime.Now;
        
        public DateTime Get() => _now;
    }
    
    class MagicDI
    {
        private class InstanceRegistry
        {
            public Type Type { get; set; }
            public Lifetime Lifetime { get; set; }
            public object Value { get; set; }
        }

        private enum Lifetime
        {
            Scoped,
            Transient,
            Singleton
        }
        
        private readonly Dictionary<Type, InstanceRegistry> _registeredInstances = new Dictionary<Type, InstanceRegistry>();

        public T Resolve<T>() => (T)Resolve(typeof(T));
        
        private object Resolve(Type type)
        {
            // 0. Check if we have already resolved the instance
            if (_registeredInstances.TryGetValue(type, out var instanceRegistry))
            {
                switch (instanceRegistry.Lifetime)
                {
                    case Lifetime.Scoped:
                        return ResolveInstance(type);
                    case Lifetime.Transient:
                        return ResolveInstance(type);
                    case Lifetime.Singleton:
                        return instanceRegistry.Value;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var resolvedInstance = ResolveInstance(type);
            
            // 4. Determine life time (Scoped, Transient, Singleton)
            var lifetime = DetermineLifeTime(resolvedInstance);
            
            // 5. Register created instance
            var registry = new InstanceRegistry { Type = type, Lifetime = lifetime, Value = resolvedInstance };
            _registeredInstances.Add(type, registry);
            
            // 6. Return created instance
            return registry.Value;
        }

        private object ResolveInstance(Type type)
        {
            if (type.IsPrimitive)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {type.Name} because it is a primitive type");

            // 1. Find the most appropriate constructor
            ConstructorInfo constructorInfo = GetConstructor(type);

            // 2. Resolve arguments to said constructor
            object[] resolvedConstructorArguments = ResolveConstructorArguments(constructorInfo);

            // 3. Invoke constructor
            var instance = constructorInfo.Invoke(resolvedConstructorArguments);

            return instance;
        }

        private ConstructorInfo GetConstructor(Type type)
        {
            var appropriateConstructor = type.GetConstructors().OrderByDescending(info => info.GetParameters().Length).FirstOrDefault();

            return appropriateConstructor;
        }

        private object[] ResolveConstructorArguments(ConstructorInfo constructorInfo)
        {
            return constructorInfo
                .GetParameters()
                .Select(info => info.ParameterType)
                .Select(Resolve)
                .ToArray();
        }

        private Lifetime DetermineLifeTime(object instance)
        {
            return Lifetime.Singleton;
        }
    }
}