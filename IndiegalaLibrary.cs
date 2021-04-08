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

namespace IndiegalaLibrary
{
    public class IndiegalaLibrary : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        public override Guid Id { get; } = Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954");

        private IndiegalaLibrarySettings PluginSettings { get; set; }

        // Change to something more appropriate
        public override string Name => "Indiegala";

        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new IndieglaClient();

        private const string dbImportMessageId = "indiegalalibImportError";

        public static bool IsLibrary = false;

        public string PluginFolder { get; set; }


        public IndiegalaLibrary(IPlayniteAPI api) : base(api)
        {
            PluginSettings = new IndiegalaLibrarySettings(this);

            // Get plugin's location 
            PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Set the common resourses & event
            Common.Load(PluginFolder, PlayniteApi.ApplicationSettings.Language);
            Common.SetEvent(PlayniteApi);
        }


        public override IEnumerable<GameInfo> GetGames()
        {
            var PlayniteDb = PlayniteApi.Database.Games;
            List<GameInfo> allGamesFinal = new List<GameInfo>();
            List<GameInfo> allGames = new List<GameInfo>();
            Exception importError = null;

            IsLibrary = true;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            IndiegalaAccountClient IndiegalaApi = new IndiegalaAccountClient(view);

            if (IndiegalaApi.GetIsUserLoggedIn())
            {
                try
                {
                    allGames = IndiegalaApi.GetOwnedGames();
                    Common.LogDebug(true, $"Found {allGames.Count} games");
                }
                catch (Exception ex)
                {
                    importError = ex;
                }
            }
            else
            {
                Exception ex = null;

                if (IndiegalaApi.GetIsUserLocked())
                {
                    ex = new Exception(resources.GetString("LOCIndiegalaLockedError"));
                }
                else
                {
                    ex = new Exception(resources.GetString("LOCNotLoggedInError"));
                }

                importError = ex;
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
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    dbImportMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }

            logger.Info($"Added: {allGamesFinal.Count()} - Already added: {allGames.Count() - allGamesFinal.Count()}");

            return allGamesFinal;
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new IndiegalaMetadataProvider(this, PlayniteApi);
        }


        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IndiegalaLibrarySettingsView(PlayniteApi, PluginSettings);
        }


        public override IGameController GetGameController(Game game)
        {
            return new IndiegalaGameController(game, this, PluginSettings);
        }
    }
}
