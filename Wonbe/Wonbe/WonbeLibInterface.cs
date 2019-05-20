using CommonLanguageInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Wonbe
{
    public class WonbeEnviroment : LanguageBaseEnvironmentInfo
    {
        public override async Task<string> LineInputAsync(string prompt)
        {
            await OutputStringAsync(prompt);
            //await WriteLineAsync();
            return await Console.In.ReadLineAsync();
        }

        public async override Task<bool> LocateAsync(int x, int y)
        {
            if (x < 0) return false;
            if (y < 0) return false;
            if (x >= Console.BufferWidth) return false;
            if (y >= Console.BufferHeight) return false;
            Console.SetCursorPosition(x, y);
            await Task.Delay(0);    // dummy
            return true;
        }

        public override async Task OutputCharAsync(char ch)
        {
            await Console.Out.WriteAsync(ch);
        }

        public override async Task OutputStringAsync(string str)
        {
            await Console.Out.WriteAsync(str);
        }

        public override async Task DebugOutputStringAsync(string str)
        {
            var oldFore = Console.ForegroundColor;
            var oldBack = Console.BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.BackgroundColor = ConsoleColor.Red;
            await Console.Out.WriteAsync(str);
            Console.ForegroundColor = oldFore;
            Console.BackgroundColor = oldBack;
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

        private LanguageBaseColor consoleColorToLanguageBaseColor(ConsoleColor cc)
        {
            var r = new LanguageBaseColor();
            byte v;
            if ((int)cc >= 8) v = 255; else v = 128;
            if (((int)cc & 1) != 0) r.B = v;
            if (((int)cc & 2) != 0) r.G = v;
            if (((int)cc & 4) != 0) r.R = v;
            r.A = 255;
            return r;
        }

        private ConsoleColor languageBaseColorToConsoleColor(LanguageBaseColor lgb)
        {
            int r = 0;
            if (lgb.B >= 256 / 3) r |= 1;
            if (lgb.G >= 256 / 3) r |= 2;
            if (lgb.R >= 256 / 3) r |= 4;
            int max = Math.Max(lgb.B, Math.Max(lgb.G, lgb.R));
            if (max < 256 / 3) r = 0;
            else if (max < 256 / 3 * 2)
            { /* do nothing */ }
            else r += 8;
            return (ConsoleColor)r;
        }

        public async override Task<LanguageBaseColor> SetForeColorAsync(LanguageBaseColor color)
        {
            await Task.Delay(0);    // dummy
            var old = Console.ForegroundColor;
            if (color != null) Console.ForegroundColor = languageBaseColorToConsoleColor(color);
            return consoleColorToLanguageBaseColor(old);
        }

        public async override Task<LanguageBaseColor> SetBackColorAsync(LanguageBaseColor color)
        {
            await Task.Delay(0);    // dummy
            var old = Console.BackgroundColor;
            if (color != null) Console.BackgroundColor = languageBaseColorToConsoleColor(color);
            return consoleColorToLanguageBaseColor(old);
        }

        public override async Task YieldAsync() => await Task.FromResult(false);

        public async override Task ClearScreen()
        {
            Console.Clear();
            await Task.Delay(0);    // dummy
        }

        public async override Task<int> GetTextWidthAsync()
        {
            await Task.Delay(0);    // dummy
            return Console.BufferWidth;
        }

        public async override Task<int> GetTextHeightAsync()
        {
            await Task.Delay(0);    // dummy
            return Console.BufferHeight;
        }

        public async override Task BeepAsync()
        {
            await Task.Delay(0);    // dummy
            Console.Beep();
        }
    }

    public class WonbeLanguageBase : AbastractLanguageBase
    {
        public override async Task InvokeInterpreterAsync(LanguageBaseStartInfo startInfo, Func<bool> breakFlagGetter, Action<bool> breakFlagSetter)
        {
            var instance = new WonbeLib.Wonbe(Environment, breakFlagGetter, breakFlagSetter);
            await instance.SuperMain(startInfo.RunRequest, startInfo.SourceCodeFileName);
        }

        public override Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo) => throw new NotImplementedException();
    }
}
