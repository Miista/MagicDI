using System;
using System.Threading;
using MagicDI;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("MagicDI Demo");
            Console.WriteLine("============");

            var di = new MagicDI.MagicDI();

            var service = di.Resolve<SomeService>();
            Console.WriteLine($"First resolve: {service.SomeMethod()}");

            Thread.Sleep(1000);

            var service1 = di.Resolve<SomeService>();
            Console.WriteLine($"Second resolve: {service1.SomeMethod()}");

            Thread.Sleep(1000);

            var service2 = di.Resolve<SomeService>();
            Console.WriteLine($"Third resolve: {service2.SomeMethod()}");

            Console.WriteLine();
            Console.WriteLine($"All instances are the same (Singleton): {ReferenceEquals(service, service1) && ReferenceEquals(service1, service2)}");
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
            Console.WriteLine($"  DateProvider timestamp: {_time.Get()}");
            return true;
        }
    }

    class DateProvider
    {
        private readonly DateTime _now = DateTime.Now;

        public DateTime Get() => _now;
    }
}
