using System;
using System.Threading;
using System.Threading.Tasks;
using CommonLanguageInterface;
using WonbeLib;

namespace WonbeFW
{
    class Program
    {
        internal static bool IsCanceled { get; set; } = false;
        [STAThread]
        static void Main(string[] args)
        {
            AutoResetEvent termEvent = new AutoResetEvent(false);
            var thread = new Thread(async () =>
            {
                try
                {
                    Console.WriteLine($"*** Welcome, {WonbeLib.Wonbe.GetMyName()} Command Line ***");
                    var info = new LanguageBaseStartInfo();
                    info.CommandLine = Environment.CommandLine;
                    if (args.Length > 0) info.SourceCodeFileName = args[0];
                    info.RunRequest = args.Length > 0;
                    var env = new WonbeEnviroment();
                    var b = new WonbeLanguageBase();
                    b.Environment = env;
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        IsCanceled = true;
                        e.Cancel = true;
                    };
                    await b.InvokeInterpreterAsync(info, () => IsCanceled, (v) => IsCanceled = v);
                    //Thread.Sleep(10000);
                }
                finally
                {
                    termEvent.Set();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            termEvent.WaitOne();
        }
    }
}
