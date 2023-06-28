using System;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }

    class MagicDI
    {
        public T Resolve<T>()
        {
            if (typeof(T).IsPrimitive)
                throw new InvalidOperationException(
                    $"Cannot resolve instance of type {typeof(T).Name} because it is a primitive type");
            
            // 1. Find the most appropriate constructor
            // 2. Resolve arguments to said constructor
            // 3. Invoke constructor
            // 4. Return created object

            return default(T);
        }
    }
}