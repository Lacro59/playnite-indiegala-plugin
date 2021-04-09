using CommonPluginsPlaynite.Common.Web;
using IndiegalaLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using CommonPluginsPlaynite.Common;
using IndiegalaLibrary.Views;
using System.Windows;
using Playnite.SDK.Plugins;
using System.Threading;
using CommonPlayniteShared.Common;

namespace IndiegalaLibrary.Services
{
    // TODO Add game icon if missing
    public class IndiegalaLibraryInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IndiegalaLibrary Plugin;
        private IndiegalaLibrarySettings Settings;


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
            GameAction DownloadAction = Game.GameActions.Where(x => x.Name == "Download").FirstOrDefault();
            if (DownloadAction == null)
            {
                logger.Warn($"No download action for {Game.Name}");
                StopInstall();
                return;
            }


            string DownloadUrl = DownloadAction.Path;
            if (DownloadUrl.IsNullOrEmpty())
            {
                logger.Warn($"No download url for {Game.Name}");
                StopInstall();
                return;
            }


            string InstallPath = Settings.InstallPath;
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
                        extractPath = Path.Combine(InstallPath, Paths.GetSafeFilename(Game.Name));
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
                            string exe = Path.Combine
                            (
                                IndiegalaLibraryExeSelection.executableInfo.Path,
                                IndiegalaLibraryExeSelection.executableInfo.Name
                            );

                            Game.GameActions.Add(new GameAction
                            {
                                Type = GameActionType.File,
                                Name = IndiegalaLibraryExeSelection.executableInfo.NameWithoutExtension,
                                Path = exe,
                                IsPlayAction = true
                            });

                            var installInfo = new GameInfo
                            {
                                InstallDirectory = Game.InstallDirectory,
                                GameActions = Game.GameActions.ToList()
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
            InvokeOnInstalled(new GameInstalledEventArgs(new GameInfo()));
            Game.IsInstalled = false;
            Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                Plugin.PlayniteApi.Database.Games.Update(Game);
            });
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
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public IndiegalaLibraryPlayController(Game game) : base(game)
        {

        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            
        }

        private void ProcMon_TreeStarted(object sender, EventArgs args)
        {
            InvokeOnStarted(new GameStartedEventArgs());
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch.Stop();
            InvokeOnStopped(new GameStoppedEventArgs() { SessionLength = Convert.ToInt64(stopWatch.Elapsed.TotalSeconds) });
        }
    }
}
