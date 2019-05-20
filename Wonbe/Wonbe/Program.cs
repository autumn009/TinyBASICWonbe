using CommonLanguageInterface;
using System;
using System.Threading.Tasks;
using WonbeLib;

namespace Wonbe
{
    class Program
    {
        internal static bool IsCanceled { get; set; } = false;
        static async Task Main(string[] args)
        {
            Console.WriteLine($"*** Welcome, {WonbeLib.Wonbe.GetMyName()} Command Line ***");
            var info = new LanguageBaseStartInfo();
            info.CommandLine = Environment.CommandLine;
            if (args.Length > 0) info.SourceCodeFileName = args[0];
            info.RunRequest = args.Length > 0;
            var env = new WonbeEnviroment();
            var b = new WonbeLanguageBase();
            b.Environment = env;
            Console.CancelKeyPress += async (sender, e) =>
            {
                IsCanceled = true;
                e.Cancel = true;
            };
            await b.InvokeInterpreterAsync(info, ()=>IsCanceled, (b)=>IsCanceled = b);
        }
    }
}
