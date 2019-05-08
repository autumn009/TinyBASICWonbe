using CommonLanguageInterface;
using System;
using System.Threading.Tasks;

namespace WonbeLib
{
    public class WonbeEnviroment : LanguageBaseEnvironmentInfo
    {
        public override Task<string> LineInputAsync(string prompt)
        {
            throw new NotImplementedException();
        }

        public override Task LocateAsync(int x, int y)
        {
            throw new NotImplementedException();
        }

        public override Task OutputCharAsync(char ch)
        {
            throw new NotImplementedException();
        }

        public override Task OutputStringAsync(string str)
        {
            throw new NotImplementedException();
        }

        public override Task SetColorAsync(LanguageBaseColor color)
        {
            throw new NotImplementedException();
        }
    }

    public class WonbeLanguageBase : AbastractLanguageBase
    {
        public override Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo)
        {
            throw new NotImplementedException();
        }

        public override Task InvokeInterpreterAsync(LanguageBaseStartInfo startInfo) => throw new NotImplementedException();
    }
}
