using IndiegalaLibrary.Services;
using IndiegalaLibrary.Views;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using CommonPluginsShared;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace IndiegalaLibrary
{
    public class IndiegalaLibrary : LibraryPlugin
    {
        private static ILogger Logger => LogManager.GetLogger();
        private static IResourceProvider ResourceProvider => new ResourceProvider();

        public override Guid Id => Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954");

        private IndiegalaLibrarySettingsViewModel PluginSettings { get; set; }

        public override string Name => "Indiegala";
        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public override LibraryClient Client => new IndieglaClient();

        private const string dbImportMessageId = "indiegalalibImportError";

        public static bool IsLibrary { get; set; } = false;
        public string PluginFolder { get; set; }


        public IndiegalaLibrary(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = true };
            PluginSettings = new IndiegalaLibrarySettingsViewModel(this);

            // Get plugin's location 
            PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Set the common resourses & event
            Common.Load(PluginFolder, PlayniteApi.ApplicationSettings.Language);
            Common.SetEvent(PlayniteApi);
        }


        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            IItemCollection<Game> PlayniteDb = PlayniteApi.Database.Games;
            List<GameMetadata> allGamesFinal = new List<GameMetadata>();
            List<GameMetadata> allGames = new List<GameMetadata>();
            Exception importError = null;

            IsLibrary = true;

            IndiegalaAccountClient indiegalaAccountClient = new IndiegalaAccountClient();

            ConnectionState state = indiegalaAccountClient.GetIsUserLoggedInWithoutClient();
            switch (state)
            {
                case ConnectionState.Locked:
                    importError = new Exception(ResourceProvider.GetString("LOCIndiegalaLockedError"));
                    break;

                case ConnectionState.Unlogged:
                    importError = new Exception(ResourceProvider.GetString("LOCNotLoggedInError"));
                    break;

                case ConnectionState.Logged:
                    try
                    {
                        allGames = indiegalaAccountClient.GetOwnedGames(this, PluginSettings);
                        Common.LogDebug(true, $"Found {allGames.Count} games");
                    }
                    catch (Exception ex)
                    {
                        importError = ex;
                    }
                    break;

                default:
                    break;
            }


            // is already add ?
            try
            {
                allGames.ForEach(x => 
                {
                    Game gameFinded = PlayniteDb.Where(y => y.GameId == x.GameId)?.FirstOrDefault();
                    if (gameFinded == null)
                    {
                        allGamesFinal.Add(x);
                        Common.LogDebug(true, $"Added: {x.Name} - {x.GameId}");
                    }
                    else
                    {
                        // Update OtherActions
                        if ((gameFinded.GameActions == null || gameFinded.GameActions.Count == 0) && x.GameActions.Count > 0)
                        {
                            gameFinded.GameActions = new ObservableCollection<GameAction> { x.GameActions[0] };                            
                        }

                        // Updated installation status
                        gameFinded.IsInstalled = x.IsInstalled;

                        Common.LogDebug(true, $"Already added: {x.Name} - {x.GameId}");
                        PlayniteDb.Update(gameFinded);
                    }
                });
            }
            catch (Exception ex)
            {
                importError = ex;
            }


            if (importError != null)
            {
                if (state == ConnectionState.Locked)
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        dbImportMessageId,
                        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                        Environment.NewLine + importError.Message,
                        NotificationType.Error,
                        () => OpenProfilForUnlocked()));
                }
                else
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        dbImportMessageId,
                        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                        Environment.NewLine + importError.Message,
                        NotificationType.Error,
                        () => OpenSettingsView()));
                }
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }


            Logger.Info($"Added: {allGamesFinal.Count()} - Already added: {allGames.Count() - allGamesFinal.Count()}");
            return allGamesFinal;
        }


        public static void OpenProfilForUnlocked()
        {
            using (IWebView WebView = API.Instance.WebViews.CreateView(670, 670))
            {
                WebView.Navigate("https://www.indiegala.com/login");
                _ = WebView.OpenDialog();
            }
        }


        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new IndiegalaMetadataProvider(this, PluginSettings.Settings);
        }


        #region Library actions
        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new IndiegalaLibraryInstallController(this, PluginSettings.Settings, args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new IndiegalaLibraryUninstallController(this, args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            yield break;
        }
        #endregion  


        #region Settings
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IndiegalaLibrarySettingsView(PlayniteApi, PluginSettings.Settings);
        }
        #endregion  
    }
}
