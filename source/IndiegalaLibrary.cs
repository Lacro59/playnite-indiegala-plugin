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
using CommonPlayniteShared.Common;
using IndiegalaLibrary.Models;
using Playnite.SDK.Data;
using System.Windows;

namespace IndiegalaLibrary
{
    public class IndiegalaLibrary : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        public override Guid Id { get; } = Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954");

        private IndiegalaLibrarySettingsViewModel PluginSettings { get; set; }

        public override string Name => "Indiegala";
        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public override LibraryClient Client { get; } = new IndieglaClient();

        private const string dbImportMessageId = "indiegalalibImportError";

        public static bool IsLibrary = false;
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
            var PlayniteDb = PlayniteApi.Database.Games;
            List<GameMetadata> allGamesFinal = new List<GameMetadata>();
            List<GameMetadata> allGames = new List<GameMetadata>();
            Exception importError = null;

            IsLibrary = true;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            IndiegalaAccountClient IndiegalaApi = new IndiegalaAccountClient(view);

            var state = IndiegalaApi.GetIsUserLoggedInWithoutClient();

            switch (state)
            {
                case ConnectionState.Locked:
                    importError = new Exception(resources.GetString("LOCIndiegalaLockedError"));
                    break;

                case ConnectionState.Unlogged:
                    importError = new Exception(resources.GetString("LOCNotLoggedInError"));
                    break;

                case ConnectionState.Logged:
                    try
                    {
                        allGames = IndiegalaApi.GetOwnedGames(this, PluginSettings);
                        Common.LogDebug(true, $"Found {allGames.Count} games");
                    }
                    catch (Exception ex)
                    {
                        importError = ex;
                    }
                    break;
            }


            // is already add ?
            try
            {
                for (int i = 0; i < allGames.Count; i++)
                {
                    if (PlayniteDb.Where(x => x.GameId == allGames[i].GameId).Count() == 0)
                    {
                        allGamesFinal.Add(allGames[i]);
                        Common.LogDebug(true, $"Added: {allGames[i].Name} - {allGames[i].GameId}");
                    }
                    else
                    {
                        Common.LogDebug(true, $"Already added: {allGames[i].Name} - {allGames[i].GameId}");

                        // Update OtherActions
                        var game = PlayniteDb.Where(x => x.GameId == allGames[i].GameId).First();
                        if ((game.GameActions == null || game.GameActions.Count == 0) && allGames[i].GameActions.Count > 0)
                        {
                            Common.LogDebug(true, $"Update OtherActions");
                            game.GameActions = new System.Collections.ObjectModel.ObservableCollection<GameAction> { allGames[i].GameActions[0] };
                            PlayniteDb.Update(game);
                        }
                    }
                }
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
                        System.Environment.NewLine + importError.Message,
                        NotificationType.Error,
                        () => OpenProfilForUnlocked()));
                }
                else
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        dbImportMessageId,
                        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                        System.Environment.NewLine + importError.Message,
                        NotificationType.Error,
                        () => OpenSettingsView()));
                }
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }


            logger.Info($"Added: {allGamesFinal.Count()} - Already added: {allGames.Count() - allGamesFinal.Count()}");
            return allGamesFinal;
        }


        private void OpenProfilForUnlocked()
        {
            using (var WebView = PlayniteApi.WebViews.CreateView(490, 670))
            {
                WebView.Navigate("https://www.indiegala.com/login");
                WebView.OpenDialog();
            }
        }


        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new IndiegalaMetadataProvider(this, PlayniteApi, PluginSettings.Settings);
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
