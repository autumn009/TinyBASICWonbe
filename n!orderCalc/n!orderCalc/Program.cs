using System;
using System.Threading.Tasks;

namespace n_orderCalc
{
    class Program
    {
        private static TimeSpan sum = new TimeSpan();
        private static void test(int n)
        {
            for (int i = 0; i < n; i++)
            {
                test(n - 1);
            }
            sum += TimeSpan.FromMilliseconds(100 * n);
        }

        static void Main()
        {
            for (int i = 2; i < 13; i++)
            {
                sum = new TimeSpan();
                test(i);
                Console.WriteLine($"{i}={sum:d'日'hh'時間'mm'分'ss'秒'}");
            }
        }
    }
}
