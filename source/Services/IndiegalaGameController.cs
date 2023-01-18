using CommonPlayniteShared.Common.Web;
using IndiegalaLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using CommonPlayniteShared.Common;
using IndiegalaLibrary.Views;
using System.Windows;
using Playnite.SDK.Plugins;
using CommonPlayniteShared.Common.Media.Icons;
using Paths = CommonPlayniteShared.Common.Paths;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaLibraryInstallController : InstallController
    {
        private static ILogger logger => LogManager.GetLogger();
        private static IResourceProvider resources => new ResourceProvider();

        private IndiegalaLibrary Plugin { get; set; }
        private IndiegalaLibrarySettings Settings { get; set; }


        public IndiegalaLibraryInstallController(IndiegalaLibrary Plugin, IndiegalaLibrarySettings Settings, Game game) : base(game)
        {
            this.Plugin = Plugin;
            this.Settings = Settings;
        }

        public override void Dispose()
        {
            
        }

        public override void Install(InstallActionArgs args)
        {
            GameAction DownloadAction = Game.GameActions?.Where(x => x.Name == "Download").FirstOrDefault();
            if (DownloadAction == null)
            {
                logger.Warn($"No download action for {Game.Name}");
                StopInstall();
                return;
            }


            string InstallPath = Settings.InstallPath;
            if (Settings.UseClient)
            {
                InstallPath = IndieglaClient.GameInstallPath;
            }


            // Check in client
            if (Settings.UseClient)
            {
                string ProdSluggedName = IndiegalaAccountClient.GetProdSluggedName(Plugin.PlayniteApi, Game.GameId);

                ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(Plugin.PlayniteApi, Game.GameId);
                if (clientGameInfo != null)
                {
                    string PathDirectory = Path.Combine(InstallPath, ProdSluggedName);
                    string ExeFile = clientGameInfo.exe_path;
                    if (ExeFile.IsNullOrEmpty() && Directory.Exists(PathDirectory))
                    {
                        var fileEnumerator = new SafeFileEnumerator(PathDirectory, "*.exe", SearchOption.AllDirectories);
                        foreach (var file in fileEnumerator)
                        {
                            ExeFile = Path.GetFileName(file.FullName);
                        }
                    }

                    string PathFolder = Path.Combine(PathDirectory, ExeFile);
                    if (File.Exists(PathFolder))
                    {
                        Game.InstallDirectory = PathDirectory;
                        Game.IsInstalled = true;

                        if (Game.GameActions != null)
                        {
                            Game.GameActions.Add(new GameAction
                            {
                                IsPlayAction = true,
                                Name = Path.GetFileNameWithoutExtension(ExeFile),
                                WorkingDir = "{InstallDir}",
                                Path = ExeFile
                            });
                        }
                        else
                        {
                            var gameActions = new List<GameAction>();
                            gameActions.Add(new GameAction
                            {
                                IsPlayAction = true,
                                Name = Path.GetFileNameWithoutExtension(ExeFile),
                                WorkingDir = "{InstallDir}",
                                Path = ExeFile
                            });

                            Game.GameActions = gameActions.ToObservable();
                        }

                        if (Game.Icon.IsNullOrEmpty())
                        {
                            GetExeIcon(Path.Combine(PathDirectory, ExeFile));
                        }

                        var installInfo = new GameInstallationData
                        {
                            InstallDirectory = Game.InstallDirectory
                            //GameActions = Game.GameActions.ToList()
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }
                }
            }


            string DownloadUrl = DownloadAction.Path;
            if (DownloadUrl.IsNullOrEmpty())
            {
                logger.Warn($"No download url for {Game.Name}");
                StopInstall();
                return;
            }


            if (InstallPath.IsNullOrEmpty() || !Directory.Exists(InstallPath))
            {
                Plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                     "IndiegalaLibrary-NoInstallationDirectory",
                     "IndiegalaLibrary" + System.Environment.NewLine + resources.GetString("LOCIndiegalaNotInstallationDirectory"),
                     NotificationType.Error,
                     () => Plugin.OpenSettingsView()));

                logger.Warn($"No InstallPath for {Game.Name}");

                StopInstall();
                return;
            }


            string FileName = Path.GetFileName(DownloadUrl);
            string FilePath = Path.Combine(Path.GetTempPath(), FileName);

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"IndiegalaLibrary - {resources.GetString("LOCDownloadingLabel")}",
                true
            );
            globalProgressOptions.IsIndeterminate = true;

            Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    Downloader downloader = new Downloader();
                    downloader.DownloadFile(DownloadUrl, FilePath, activateGlobalProgress.CancelToken);

                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        StopInstall();
                        return;
                    }

                    bool HasError = false;
                    string extractPath = string.Empty;
                    try
                    {
                        int.TryParse(Game.GameId, out int IntId);
                        string prod_slugged_name = Paths.GetSafePathName(Game.Name);

                        // Find prod_slugged_name for Showcase to respect client normalisation installation
                        if (IntId != 0)
                        {
                            prod_slugged_name = IndiegalaAccountClient.GetProdSluggedName(Plugin.PlayniteApi, Game.GameId);
                        }

                        if (!Directory.Exists(InstallPath))
                        {
                            Directory.CreateDirectory(InstallPath);
                        }

                        extractPath = Path.Combine(InstallPath, Paths.GetSafePathName(prod_slugged_name));
                        ZipFile.ExtractToDirectory(FilePath, extractPath);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                        Plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                             "IndiegalaLibrary-ZipError",
                             "IndiegalaLibrary" + System.Environment.NewLine + ex.Message,
                             NotificationType.Error));
                        HasError = true;
                    }
                    finally
                    {
                        File.Delete(FilePath);
                    }


                    if (!HasError)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            var ViewExtension = new IndiegalaLibraryExeSelection(extractPath);
                            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(Plugin.PlayniteApi, resources.GetString("LOCIndiegalaLibraryExeSelectionTitle"), ViewExtension);
                            windowExtension.ShowDialog();
                        }).Wait();


                        Game.InstallDirectory = extractPath;
                        Game.IsInstalled = true;


                        if (IndiegalaLibraryExeSelection.executableInfo != null)
                        {
                            string exePath = Path.Combine
                            (
                                IndiegalaLibraryExeSelection.executableInfo.Path,
                                IndiegalaLibraryExeSelection.executableInfo.Name
                            );

                            Game.GameActions.Add(new GameAction
                            {
                                Type = GameActionType.File,
                                Name = IndiegalaLibraryExeSelection.executableInfo.NameWithoutExtension,
                                Path = exePath,
                                IsPlayAction = true
                            });


                            if (Game.Icon.IsNullOrEmpty())
                            {
                                GetExeIcon(exePath);
                            }

                            var installInfo = new GameInstallationData
                            {
                                InstallDirectory = Game.InstallDirectory
                                //GameActions = Game.GameActions.ToList()
                            };

                            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        }
                    }
                    else
                    {
                        StopInstall();
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                    StopInstall();
                }

                return;
            }, globalProgressOptions);
        }


        private void StopInstall()
        {
            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData()));
            Game.IsInstalled = false;
            Application.Current.Dispatcher?.BeginInvoke((Action)delegate
            {
                Plugin.PlayniteApi.Database.Games.Update(Game);
            });
        }

        private string GetExeIcon(string exePath)
        {
            string convertedPath = null;
            try
            {
                string GameFilesPath = Path.Combine(Plugin.PlayniteApi.Database.GetFullFilePath(""), Game.Id.ToString());
                convertedPath = Path.Combine(GameFilesPath, Guid.NewGuid() + ".ico");

                if (IconExtractor.ExtractMainIconFromFile(exePath, convertedPath))
                {
                    Game.Icon = convertedPath;
                    Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                    {
                        Plugin.PlayniteApi.Database.Games.Update(Game);
                    }).Wait();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return convertedPath;
        }
    }

    public class IndiegalaLibraryUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IndiegalaLibrary Plugin;


        public IndiegalaLibraryUninstallController(IndiegalaLibrary Plugin, Game game) : base(game)
        {
            this.Plugin = Plugin;
            Name = "Uninstall";
        }

        public override void Dispose()
        {

        }

        public override void Uninstall(UninstallActionArgs args)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"IndiegalaLibrary - {resources.GetString("LOCUninstalling")}",
                true
            );
            globalProgressOptions.IsIndeterminate = true;

            Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                {
                    return;
                }


                try
                {
                    FileSystem.DeleteDirectory(Game.InstallDirectory);
                    RemovePlayAction();
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                    Plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                            "IndiegalaLibrary-UninstallError",
                            "IndiegalaLibrary" + System.Environment.NewLine + ex.Message,
                            NotificationType.Error));
                }

                return;
            }, globalProgressOptions);
        }


        private void RemovePlayAction()
        {
            Game.GameActions = Game.GameActions.Where(x => !x.IsPlayAction).ToObservable();

            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                Plugin.PlayniteApi.Database.Games.Update(Game);
            });
        }
    }

    public class IndiegalaLibraryPlayController : PlayController
    {
        public IndiegalaLibraryPlayController(Game game) : base(game)
        {

        }

        public override void Dispose()
        {
        }

        public override void Play(PlayActionArgs args)
        {
            
        }
    }
}
