using CommonLanguageInterface;
using System;
using System.Threading.Tasks;

namespace WonbeLib
{
    public class WonbeEnviroment : LanguageBaseEnvironmentInfo
    {
        public override async Task<string> LineInputAsync(string prompt)
        {
            await OutputStringAsync(prompt);
            await WriteLineAsync();
            return await Console.In.ReadLineAsync();
        }

        public override Task LocateAsync(int x, int y)
        {
            throw new NotImplementedException();
        }

        public override async Task OutputCharAsync(char ch)
        {
            await Console.Out.WriteAsync(ch);
        }

        public override async Task OutputStringAsync(string str)
        {
            await Console.Out.WriteAsync(str);
        }

        public override Task SetColorAsync(LanguageBaseColor color)
        {
            throw new NotImplementedException();
        }

        public override async Task YieldAsync() => await Task.FromResult(false);
    }

    public class WonbeLanguageBase : AbastractLanguageBase
    {
        public override async Task InvokeInterpreterAsync(LanguageBaseStartInfo startInfo)
        {
            if (startInfo.RunRequest)
            {
                await Wonbe.RunProgramAsync(startInfo.SourceCodeFileName, Environment);
            }
            else
            {
                await Wonbe.RunInteractiveAsync(Environment);
            }
        }

        public override Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo) => throw new NotImplementedException();
    }
}
