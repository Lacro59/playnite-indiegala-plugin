using Playnite.SDK;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;

namespace IndiegalaLibrary.Services
{
    public class IndieglaClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public override bool IsInstalled => false;

        public override void Open()
        {
            Process.Start("https://www.indiegala.com/");
        }

        public override void Shutdown()
        {

        }
    }
}
