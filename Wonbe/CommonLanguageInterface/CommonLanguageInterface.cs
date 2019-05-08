using System;
using System.Threading.Tasks;

namespace CommonLanguageInterface
{
    public struct LanguageBaseColor
    {
        public byte R, G, B, A;
        public LanguageBaseColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
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
        public abstract Task OutputCharAsync(char ch);
        public abstract Task LocateAsync(int x, int y);
        public abstract Task SetColorAsync(LanguageBaseColor color);
    }
    public abstract class AbastractLanguageBase
    {
        public abstract Task InvokeInterpreterAsync(LanguageBaseStartInfo startInfo);
        public abstract Task InvokeCompilerAsync(LanguageBaseStartInfo startInfo);
        public LanguageBaseEnvironmentInfo Environment { get; set; }
    }
}
