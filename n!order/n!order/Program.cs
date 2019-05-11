using System;
using System.Threading.Tasks;

namespace n_order
{
    class Program
    {
        private static async Task test(int n)
        {
            for (int i = 0; i < n; i++)
            {
                await Task.Delay(100);
                await test(n - 1);
            }
        }

        static async Task Main()
        {
            for (int i = 2; i < 6; i++)
            {
                var start = DateTimeOffset.Now;
                await test(i);
                Console.WriteLine($"{i}={DateTimeOffset.Now - start}");
            }
        }
    }
}
