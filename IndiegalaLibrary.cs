﻿using IndiegalaLibrary.Services;
using IndiegalaLibrary.Views;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;
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

        private IndiegalaLibrarySettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954");

        // Change to something more appropriate
        public override string Name => "Indiegala";

        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        // Implementing Client adds ability to open it via special menu in playnite.
        public override LibraryClient Client { get; } = new IndieglaClient();

        private const string dbImportMessageId = "indiegalalibImportError";


        public IndiegalaLibrary(IPlayniteAPI api) : base(api)
        {
            settings = new IndiegalaLibrarySettings(this);

            // Get plugin's location 
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Add plugin localization in application ressource.
            PluginCommon.Localization.SetPluginLanguage(pluginFolder, api.Paths.ConfigurationPath);
            // Add common in application ressource.
            PluginCommon.Common.Load(pluginFolder);

            // Check version
            if (settings.EnableCheckVersion)
            {
                CheckVersion cv = new CheckVersion();
            
                if (cv.Check("Indiegala", pluginFolder))
                {
                    cv.ShowNotification(api, "IndiegalaLibrary - " + resources.GetString("LOCUpdaterWindowTitle"));
                }
            }
        }

        public override IEnumerable<GameInfo> GetGames()
        {
            var PlayniteDb = PlayniteApi.Database.Games;
            List<GameInfo> allGamesFinal = new List<GameInfo>();
            List<GameInfo> allGames = new List<GameInfo>();
            Exception importError = null;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            IndiegalaAccountClient IndiegalaApi = new IndiegalaAccountClient(view);

            if (IndiegalaApi.GetIsUserLoggedIn())
            {
                try
                {
                    allGames = IndiegalaApi.GetOwnedGames();
#if DEBUG
                    logger.Debug($"IndiegalaLibrary - Found {allGames.Count} games");
#endif
                }
                catch (Exception ex)
                {
                    importError = ex;
                }
            }
            else
            {
                Exception ex = new Exception(resources.GetString("LOCNotLoggedInError"));
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

#if DEBUG
                        logger.Debug($"IndiegalaLibrary - Added: {allGames[i].Name} - {allGames[i].GameId}");
#endif
                    }
                    else
                    {
#if DEBUG
                        logger.Debug($"IndiegalaLibrary - Already added: {allGames[i].Name} - {allGames[i].GameId}");
#endif
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

            logger.Info($"IndiegalaLibrary - Added: {allGamesFinal.Count()} - Already added: {allGames.Count() - allGamesFinal.Count()}");

            return allGamesFinal;
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new IndiegalaMetadataProvider(this, PlayniteApi);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IndiegalaLibrarySettingsView(PlayniteApi, settings);
        }
    }
}
