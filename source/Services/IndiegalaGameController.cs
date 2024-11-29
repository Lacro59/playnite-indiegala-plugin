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
using CommonPluginsShared.Extensions;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaLibraryInstallController : InstallController
    {
        private static ILogger Logger => LogManager.GetLogger();

        private IndiegalaLibrary Plugin { get; set; }
        private IndiegalaLibrarySettings Settings { get; set; }


        public IndiegalaLibraryInstallController(IndiegalaLibrary plugin, IndiegalaLibrarySettings settings, Game game) : base(game)
        {
            Plugin = plugin;
            Settings = settings;
        }

        public override void Dispose()
        {
        }

        public override void Install(InstallActionArgs args)
        {
            string filePath = string.Empty;
            string extractPath = string.Empty;

            GameAction DownloadAction = Game.GameActions?.Where(x => x.Name.IsEqual(ResourceProvider.GetString("LOCDownloadLabel")) || x.Name.IsEqual("Download"))?.FirstOrDefault();
            string downloadUrl = DownloadAction?.Path;
            if (downloadUrl.IsNullOrEmpty())
            {
                Logger.Warn($"No download url for {Game.Name}");
                StopInstall(filePath, extractPath);
                return;
            }

            string InstallPath = Settings.InstallPath;
            if (Settings.UseClient && IndiegalaLibrary.IndiegalaClient.IsInstalled)
            {
                InstallPath = IndiegalaClient.GameInstallPath;
            }

            // TODO Check in client
            if (Settings.UseClient && IndiegalaLibrary.IndiegalaClient.IsInstalled)
            {
                GameAction gameAction = IndiegalaClient.GameIsInstalled(Game.GameId);
                Game.IsInstalled = gameAction != null;
                Game.InstallDirectory = gameAction?.WorkingDir;
                GameInstallationData installInfo = new GameInstallationData
                {
                    InstallDirectory = Game.InstallDirectory
                };

                InvokeOnInstalled(new GameInstalledEventArgs(installInfo)); 
                return;
            }

            if (InstallPath.IsNullOrEmpty() || !Directory.Exists(InstallPath))
            {
                API.Instance.Notifications.Add(new NotificationMessage(
                     "IndiegalaLibrary-NoInstallationDirectory",
                     "IndiegalaLibrary" + Environment.NewLine + ResourceProvider.GetString("LOCIndiegalaNotInstallationDirectory"),
                     NotificationType.Error,
                     () => Plugin.OpenSettingsView()));

                Logger.Warn($"No InstallPath for {Game.Name}");

                StopInstall(filePath, extractPath);
                return;
            }

            string fileName = Path.GetFileName(downloadUrl);
            filePath = Path.Combine(Path.GetTempPath(), fileName);

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"IndiegalaLibrary - {ResourceProvider.GetString("LOCDownloadingLabel")}")
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((a) =>
            {
                try
                {
                    Downloader downloader = new Downloader();
                    downloader.DownloadFile(downloadUrl, filePath, a.CancelToken);

                    if (a.CancelToken.IsCancellationRequested)
                    {
                        StopInstall(filePath, extractPath);
                        return;
                    }

                    bool hasError = false;
                    a.Text = $"IndiegalaLibrary - {ResourceProvider.GetString("LOCCommonExtracting")}";
                    try
                    {
                        string prod_slugged_name = IndiegalaLibrary.IndiegalaApi.GetShowcaseData(Game.GameId)?.prod_slugged_name ?? Paths.GetSafePathName(Game.Name);
                        FileSystem.CreateDirectory(InstallPath);
                        extractPath = Path.Combine(InstallPath, Paths.GetSafePathName(prod_slugged_name));
                        ZipFile.ExtractToDirectory(filePath, extractPath);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                        API.Instance.Notifications.Add(new NotificationMessage(
                             "IndiegalaLibrary-ZipError",
                             "IndiegalaLibrary" + Environment.NewLine + ex.Message,
                             NotificationType.Error));
                        hasError = true;
                    }
                    finally
                    {
                        FileSystem.DeleteFileSafe(filePath);
                    }

                    if (!hasError)
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            IndiegalaLibraryExeSelection ViewExtension = new IndiegalaLibraryExeSelection(extractPath);
                            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCIndiegalaLibraryExeSelectionTitle"), ViewExtension);
                            _ = windowExtension.ShowDialog();
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
                                Path = exePath.StartsWith(extractPath, StringComparison.InvariantCultureIgnoreCase) ? exePath.Replace(extractPath + "\\", string.Empty, StringComparison.InvariantCultureIgnoreCase) : exePath,
                                WorkingDir = exePath.StartsWith(extractPath, StringComparison.InvariantCultureIgnoreCase) ? "{InstallDir}" : string.Empty,
                                IsPlayAction = true
                            });

                            if (Game.Icon.IsNullOrEmpty())
                            {
                                Game.Icon = GetExeIcon(exePath);
                            }

                            GameInstallationData installInfo = new GameInstallationData
                            {
                                InstallDirectory = Game.InstallDirectory
                            };

                            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        }
                    }
                    else
                    {
                        StopInstall(filePath, extractPath);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                    StopInstall(filePath, extractPath);
                }

                return;
            }, globalProgressOptions);
        }


        private void StopInstall(string filePath, string extractPath)
        {
            FileSystem.DeleteFileSafe(filePath);
            FileSystem.DeleteDirectory(extractPath);
            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData()));
            Game.IsInstalled = false;
            _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
            {
                API.Instance.Database.Games.Update(Game);
            });
        }

        private string GetExeIcon(string exePath)
        {
            string convertedPath = null;
            try
            {
                string gameFilesPath = Path.Combine(API.Instance.Database.GetFullFilePath(""), Game.Id.ToString());
                convertedPath = Path.Combine(gameFilesPath, Guid.NewGuid() + ".ico");

                if (IconExtractor.ExtractMainIconFromFile(exePath, convertedPath))
                {
                    Game.Icon = convertedPath;
                    _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                    {
                        API.Instance.Database.Games.Update(Game);
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
        private IndiegalaLibrary Plugin { get; }


        public IndiegalaLibraryUninstallController(IndiegalaLibrary plugin, Game game) : base(game)
        {
            Plugin = plugin;
            Name = "Uninstall";
        }

        public override void Dispose()
        {

        }

        public override void Uninstall(UninstallActionArgs args)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"IndiegalaLibrary - {ResourceProvider.GetString("LOCUninstalling")}")
            {
                Cancelable = true,
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((a) =>
            {
                if (a.CancelToken.IsCancellationRequested)
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
                    API.Instance.Notifications.Add(new NotificationMessage(
                            "IndiegalaLibrary-UninstallError",
                            "IndiegalaLibrary" + Environment.NewLine + ex.Message,
                            NotificationType.Error));
                }

                return;
            }, globalProgressOptions);
        }

        private void RemovePlayAction()
        {
            Game.GameActions = Game.GameActions.Where(x => !x.IsPlayAction).ToObservable();
            _ = Application.Current.Dispatcher.BeginInvoke((Action)delegate
            {
                API.Instance.Database.Games.Update(Game);
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
