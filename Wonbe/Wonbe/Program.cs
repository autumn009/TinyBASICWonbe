using CommonLanguageInterface;
using System;
using System.Threading.Tasks;
using WonbeLib;

namespace Wonbe
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += (sender, e) => e.Cancel = true;
            Console.WriteLine($"*** Welcome, {WonbeLib.Wonbe.GetMyName()} Command Line ***");
            var info = new LanguageBaseStartInfo();
            info.CommandLine = Environment.CommandLine;
            if (args.Length > 0) info.SourceCodeFileName = args[0];
            info.RunRequest = args.Length > 0;
            var env = new WonbeEnviroment();
            var b = new WonbeLanguageBase();
            b.Environment = env;
            await b.InvokeInterpreterAsync(info);
        }
    }
}
