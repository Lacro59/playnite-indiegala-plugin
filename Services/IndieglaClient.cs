using Playnite.SDK;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CommonPluginsShared;
using Microsoft.Win32;
using System.Threading;

namespace IndiegalaLibrary.Services
{
    public class IndieglaClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public override bool IsInstalled => File.Exists(ClientExecPath);

        private static string _ClientExecPath = string.Empty;
        public static string ClientExecPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_ClientExecPath))
                {
                    return _ClientExecPath;
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store"))
                {
                    if (key != null)
                    {
                        foreach (var el in key?.GetValueNames())
                        {
                            if (el.Contains("IGClient") == true)
                            {
                                string path = Path.Combine(el.ToString());
                                if (File.Exists(path))
                                {
                                    _ClientExecPath = el.ToString();
                                    return el.ToString();
                                }
                            }
                        }
                    }
                }

                return string.Empty;
            }
        }


        public override void Open()
        {
            Process.Start(ClientExecPath);
        }

        public override void Shutdown()
        {
            var mainProc = Process.GetProcessesByName("IGClient").FirstOrDefault();
            if (mainProc == null)
            {
                logger.Info("IndieGala client is no longer running, no need to shut it down.");
                return;
            }

            // TODO Check when command line
            Process[] workers = Process.GetProcessesByName("IGClient");
            foreach (Process worker in workers)
            {
                worker.Kill();
                worker.WaitForExit(2000);
                worker.Dispose();
            }
        }



    }
}
