using CommonLanguageInterface;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WonbeLib
{
    public class WonbeEnviroment : LanguageBaseEnvironmentInfo
    {
        public override async Task<string> LineInputAsync(string prompt)
        {
            await OutputStringAsync(prompt);
            //await WriteLineAsync();
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

        private void setCurrentDirectory()
        {
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
        }

        public override Task FilesAsync()
        {
            setCurrentDirectory();
            throw new NotImplementedException();
        }

        public override async Task<Stream> SaveAsync(string filename)
        {
            await Task.Delay(0);    // dummy
            setCurrentDirectory();
            return File.OpenWrite(filename);
        }

        public override async Task<Stream> LoadAsync(string filename)
        {
            await Task.Delay(0);    // dummy
            setCurrentDirectory();
            if (File.Exists(filename))
                return File.OpenRead(filename);
            else
                return null;
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
            var instance = new Wonbe(Environment);
            await instance.SuperMain(startInfo.RunRequest, startInfo.SourceCodeFileName);
        }

        public override Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo) => throw new NotImplementedException();
    }
}
