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
using System.Diagnostics;
using CommonPluginsShared.Extensions;
using System.Text.RegularExpressions;

namespace IndiegalaLibrary
{
    public class IndiegalaLibrary : LibraryPlugin
    {
        private static ILogger Logger => LogManager.GetLogger();

        public override Guid Id => Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954");

        private IndiegalaLibrarySettingsViewModel PluginSettings { get; set; }

        public override string Name => "Indiegala";
        public override string LibraryIcon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public override LibraryClient Client => IndiegalaClient;

        private string DbImportMessageId => "indiegalalibImportError";

        public static bool IsLibrary { get; set; } = false;
        public string PluginFolder { get; set; }

        internal static IndiegalaApi IndiegalaApi { get; set; }
        internal static IndiegalaClient IndiegalaClient { get; set; }


        public IndiegalaLibrary(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties { CanShutdownClient = true };
            PluginSettings = new IndiegalaLibrarySettingsViewModel(this);

            // Get plugin's location 
            PluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Set the common resourses & event
            Common.Load(PluginFolder, PlayniteApi.ApplicationSettings.Language);

            IndiegalaApi = new IndiegalaApi(GetPluginUserDataPath(), false);
            IndiegalaClient = new IndiegalaClient();
        }


        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            List<GameMetadata> allGamesFinal = new List<GameMetadata>();
            List<GameMetadata> allGames = new List<GameMetadata>();
            Exception importError = null;

            IsLibrary = true;

            if (IndiegalaApi.IsUserLoggedIn)
            {
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    IEnumerable<Game> gameOwned = API.Instance.Database.Games.Where(y => y.PluginId == Id);
                    List<GameMetadata> OwnedGamesShowcase = IndiegalaApi.GetOwnedShowcase(true);
                    // TODO Don't work anymore
                    List<GameMetadata> OwnedGamesBundle = new List<GameMetadata>(); //IndiegalaApi.GetOwnedGamesBundleStore(DataType.bundle);
                    List<GameMetadata> OwnedGamesStore = new List<GameMetadata>(); //IndiegalaApi.GetOwnedGamesBundleStore(DataType.store);

                    allGames = allGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();

                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;
                    Logger.Info($"GetOwnedGames ({allGames.Count}) - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");

                    // is already add ?
                    allGames.ForEach(x =>
                    {
                        Game gameFinded = null;

                        // Change id for showcase
                        List<string> ids = x.GameId.Split('|').ToList();
                        if (ids.Count > 1)
                        {
                            gameFinded = gameOwned.Where(y => y.GameId.IsEqual(ids[0]))?.FirstOrDefault();
                            if (gameFinded != null)
                            {
                                gameFinded.GameId = ids[1];
                                API.Instance.Database.Games.Update(gameFinded);
                            }
                            x.GameId = ids[1];
                        }

                        gameFinded = gameOwned.Where(y => y.GameId.IsEqual(x.GameId))?.FirstOrDefault();
                        if (gameFinded == null)
                        {
                            allGamesFinal.Add(x);
                            Common.LogDebug(true, $"Added: {x.Name} - {x.GameId}");
                        }
                        else
                        {
                            // Update OtherActions
                            if (gameFinded.GameActions?.Count() == 0)
                            {
                                gameFinded.GameActions = x.GameActions.ToObservable();
                            }
                            else
                            {
                                _ = gameFinded.GameActions.AddMissing(x.GameActions);
                            }

                            // Update Links
                            ObservableCollection<Link> links = gameFinded.Links?
                                .Where(y => !y.Name.IsEqual(ResourceProvider.GetString("LOCMetaSourceStore")) && !y.Name.IsEqual("store") && !y.Name.IsEqual(ResourceProvider.GetString("LOCDownloadLabel")) && !y.Name.IsEqual("Download"))
                                ?.ToObservable() ?? new ObservableCollection<Link>();
                            links.Add(x.Links.First());
                            gameFinded.Links = links;

                            // Updated installation status
                            if (x.IsInstalled)
                            {
                                gameFinded.IsInstalled = x.IsInstalled;
                                gameFinded.InstallDirectory = IndiegalaClient.GameIsInstalled(gameFinded.GameId).WorkingDir;
                            }

                            Common.LogDebug(true, $"Already added: {x.Name} - {x.GameId}");
                            API.Instance.Database.Games.Update(gameFinded);
                        }
                    });
                }
                catch (Exception ex)
                {
                    importError = ex;
                }
            }
            else
            {
                IndiegalaApi.NotAuthenticated();
            }

            //ConnectionState state = indiegalaApi.GetIsUserLoggedInWithoutClient();
            //switch (state)
            //{
            //    case ConnectionState.Locked:
            //        importError = new Exception(ResourceProvider.GetString("LOCIndiegalaLockedError"));
            //        break;
            //
            //    case ConnectionState.Unlogged:
            //        importError = new Exception(ResourceProvider.GetString("LOCNotLoggedInError"));
            //        break;
            //
            //    case ConnectionState.Logged:
            //        try
            //        {
            //            allGames = indiegalaApi.GetOwnedGames(this, PluginSettings);
            //            Common.LogDebug(true, $"Found {allGames.Count} games");
            //        }
            //        catch (Exception ex)
            //        {
            //            importError = ex;
            //        }
            //        break;
            //
            //    default:
            //        break;
            //}

            if (importError != null)
            {
                //if (state == ConnectionState.Locked)
                //{
                //    PlayniteApi.Notifications.Add(new NotificationMessage(
                //        dbImportMessageId,
                //        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                //        Environment.NewLine + importError.Message,
                //        NotificationType.Error,
                //        () => OpenProfilForUnlocked()));
                //}
                //else
                //{
                //    PlayniteApi.Notifications.Add(new NotificationMessage(
                //        dbImportMessageId,
                //        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                //        Environment.NewLine + importError.Message,
                //        NotificationType.Error,
                //        () => OpenSettingsView()));
                //}
            }
            else
            {
                PlayniteApi.Notifications.Remove(DbImportMessageId);
            }


            Logger.Info($"Added: {allGamesFinal.Count()} - Already added: {allGames.Count() - allGamesFinal.Count()}");
            return allGamesFinal;
        }


        public static void OpenProfilForUnlocked()
        {
            using (IWebView webView = API.Instance.WebViews.CreateView(670, 670))
            {
                webView.Navigate("https://www.indiegala.com/login");
                _ = webView.OpenDialog();
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
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            if (!PluginSettings.Settings.UseClient || !IndiegalaClient.IsInstalled)
            {
                yield break;
            }

            GameAction gameAction = IndiegalaClient.GameIsInstalled(args.Game.GameId);
            if (gameAction != null)
            {
                string fileName = gameAction.Path;
                if (!File.Exists(Path.Combine(gameAction.WorkingDir, fileName)))
                {
                    Dictionary<char, string> arabicToRoman = new Dictionary<char, string>
                    {
                        { '1', "I" },
                        { '2', "II" },
                        { '3', "III" },
                        { '4', "IV" },
                        { '5', "V" },
                        { '6', "VI" },
                        { '7', "VII" },
                        { '8', "VIII" },
                        { '9', "IX" }
                    };
                    fileName = Regex.Replace(gameAction.Path, @"\d", match => arabicToRoman[match.Value[0]]);
                }

                yield return new AutomaticPlayController(args.Game)
                {
                    Type = AutomaticPlayActionType.File,
                    Name = "IGClient",
                    WorkingDir = gameAction.WorkingDir,
                    Path = fileName
                };
            }

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
            return new IndiegalaLibrarySettingsView(PluginSettings.Settings);
        }

        #endregion  
    }
}