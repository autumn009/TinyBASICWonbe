using CommonLanguageInterface;
using System;
using System.Collections;
using System.Collections.Generic;
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

        private string getCurrentDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        private void setCurrentDirectory() => Directory.SetCurrentDirectory(getCurrentDirectory());

        public override async Task<IEnumerable<string>> FilesAsync(string path)
        {
            await Task.Delay(0);    // dummy
            try
            {
                if (path == null) path = getCurrentDirectory();
                if (Directory.Exists(path)) return Directory.EnumerateFiles(path, "*.wb");
                var dirpart = Path.GetDirectoryName(path);
                var namepart = Path.GetFileName(path);
                if (!Path.HasExtension(namepart)) namepart = namepart + ".wb";
                if (namepart.IndexOf('*') >= 0 || namepart.IndexOf('?') >= 0) return Directory.EnumerateFiles(dirpart, namepart);
            }
            catch (FileNotFoundException)
            {
                // nop
            }
            catch (DirectoryNotFoundException)
            {
                // nop
            }
            return null;    // directory not found
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
