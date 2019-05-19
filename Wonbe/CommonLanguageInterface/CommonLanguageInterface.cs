using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CommonLanguageInterface
{
    public class LanguageBaseColor
    {
        public byte R, G, B, A;
        public LanguageBaseColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
        public LanguageBaseColor()
        {
            A = 255;
        }
    }

    /// <summary>
    /// AbastractLanguageBaseを継承した有効なクラスに付加する
    /// 使用できる言語を列挙する場合はこの属性に付いた型を列挙する
    /// </summary>
    /// [AttributeUsage(AttributeTargets.Class)]
    public class LanguageBaseAttribute : Attribute { }

    public class LanguageBaseStartInfo
    {
        public string CommandLine;
        public string SourceCodeFileName;
        public bool RunRequest;
    }
    public abstract class LanguageBaseEnvironmentInfo
    {
        public abstract Task<string> LineInputAsync(string prompt);
        public abstract Task OutputStringAsync(string str);
        public abstract Task DebugOutputStringAsync(string str);
        public abstract Task OutputCharAsync(char ch);
        public abstract Task LocateAsync(int x, int y);
        public abstract Task<LanguageBaseColor> SetForeColorAsync(LanguageBaseColor color);
        public abstract Task<LanguageBaseColor> SetBackColorAsync(LanguageBaseColor color);
        public abstract Task YieldAsync();

        public abstract Task<Stream> SaveAsync(string filename);
        public abstract Task<Stream> LoadAsync(string filename);
        public abstract Task<IEnumerable<string>> FilesAsync(string path);

        public async Task WriteLineAsync() => await OutputStringAsync("\r\n");
        public async Task WriteLineAsync(string msg, params object[] args)
        {
            await OutputStringAsync(string.Format(msg, args));
            await WriteLineAsync();
        }
    }
    public abstract class AbastractLanguageBase
    {
        public abstract Task InvokeInterpreterAsync(LanguageBaseStartInfo startInfo);
        public abstract Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo);
        public LanguageBaseEnvironmentInfo Environment { get; set; }
    }
}
