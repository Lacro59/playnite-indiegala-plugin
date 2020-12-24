using CommonPlaynite.Common.Web;
using IndiegalaLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using CommonPlaynite.Common;
using IndiegalaLibrary.Views;
using System.Windows;
using Playnite.SDK.Events;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaGameController : BaseGameController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IndiegalaLibrary _library;
        private IndiegalaLibrarySettings _settings;


        public IndiegalaGameController(Game game, IndiegalaLibrary library, IndiegalaLibrarySettings settings) : base(game)
        {
            _library = library;
            _settings = settings;
        }


        public override void Install()
        {
            var stopWatch = Stopwatch.StartNew();

            GameAction DownloadAction = Game.OtherActions.Where(x => x.Name == "Download").FirstOrDefault();
            if (DownloadAction == null)
            {
                logger.Warn($"IndiegalaLibrary - No download action for {Game.Name}");
                return;
            }


            string DownloadUrl = DownloadAction.Path;
            if (DownloadUrl.IsNullOrEmpty())
            {
                logger.Warn($"IndiegalaLibrary - No download url for {Game.Name}");
                return;
            }

            string InstallPath = _settings.InstallPath;
            if (DownloadUrl.IsNullOrEmpty())
            {
                _library.PlayniteApi.Notifications.Add(new NotificationMessage(
                     "IndiegalaLibrary-NoInstallationDirectory",
                     "IndiegalaLibrary" +  System.Environment.NewLine + resources.GetString("LOCIndiegalaNotInstallationDirectory"),
                     NotificationType.Error,
                     () => _library.OpenSettingsView()));

                logger.Warn($"IndiegalaLibrary - No download url for {Game.Name}");
                return;
            }


            string FileName = Path.GetFileName(DownloadUrl);
            string FilePath = Path.Combine(Path.GetTempPath(), FileName);

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"IndiegalaLibrary - {resources.GetString("LOCDownloadingLabel")}",
                true
            );
            globalProgressOptions.IsIndeterminate = true;

            _library.PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    Downloader downloader = new Downloader();
                    downloader.DownloadFile(DownloadUrl, FilePath, activateGlobalProgress.CancelToken);

                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
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
                        Common.LogError(ex, "IndiegalaLibrary");
                        _library.PlayniteApi.Notifications.Add(new NotificationMessage(
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
                            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(_library.PlayniteApi, resources.GetString("LOCIndiegalaLibraryExeSelectionTitle"), ViewExtension);
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

                            Game.PlayAction = new GameAction
                            {
                                Type = GameActionType.File,
                                Path = exe,
                                IsHandledByPlugin = false
                            };

                            var installInfo = new GameInfo
                            {
                                InstallDirectory = Game.InstallDirectory,
                                PlayAction = Game.PlayAction
                            };

                            
                            stopWatch.Stop();
                            OnInstalled(this, new GameInstalledEventArgs(installInfo, this, stopWatch.Elapsed.TotalSeconds));
                        }


                        _library.PlayniteApi.Database.Games.Update(Game);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IndiegalaLibrary");
                }
            }, globalProgressOptions);
        }

        public override void Play()
        {
            throw new NotImplementedException();
        }

        public override void Uninstall()
        {
            throw new NotImplementedException();
        }
    }
}
