using CommonLanguageInterface;
using System;
using WonbeLib;

namespace Wonbe
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"*** Welcome, {WonbeLib.Wonbe.GetMyName()} Command Line ***");
            var info = new LanguageBaseStartInfo();
            info.CommandLine = Environment.CommandLine;
            if (args.Length > 0) info.SourceCodeFileName = args[0];
            info.RunRequest = args.Length > 0;
            var env = new WonbeEnviroment();
            var b = new WonbeLanguageBase();
            b.Environment = env;
            b.InvokeInterpreterAsync(null);
        }
    }
}
