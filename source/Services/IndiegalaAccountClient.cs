using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK;
using Playnite.SDK.Models;
using CommonPluginsShared;
using System;
using System.Linq;
using System.Collections.Generic;
using IndiegalaLibrary.Models;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using Playnite.SDK.Data;
using CommonPlayniteShared.Common;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using AngleSharp.Dom;
using CommonPluginsShared.Extensions;
using CommonPlayniteShared;
using System.Text;
using System.Security.Principal;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaAccountClient
    {
        private static ILogger Logger => LogManager.GetLogger();

        #region Url
        private static string BaseUrl => "https://www.indiegala.com";
        private string LoginUrl => BaseUrl + "/login";
        private string LogoutUrl => BaseUrl + "/logout";
        private string LibraryUrl => BaseUrl + "/library";
        private string ShowcaseUrl => BaseUrl + "/library/showcase/{0}";
        private string BundleUrl => BaseUrl + "/library/bundle/{0}";
        private string StoreUrl => BaseUrl + "/library/store/{0}";
        private static string StoreSearch => BaseUrl + "/search/query";
        private static string ShowcaseSearch => BaseUrl + "/showcase/ajax/{0}";

        private static string UrlGetStore => BaseUrl + "/library/get-store-contents";
        private static string UrlGetBundle => BaseUrl + "/library/get-bundle-contents";

        private static string ApiUrl => BaseUrl + "/login_new/user_info";

        private static string ProdCoverUrl => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodcover/{2}";
        private static string ProdMainUrl => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodmain/{2}";
        #endregion

        public bool IsConnected { get; set; } = false;
        public bool IsLocked { get; set; } = false;

        private static List<UserCollection> UserCollections { get; set; } = new List<UserCollection>();


        #region Client
        public static string GetProdSluggedName(string gameId)
        {
            //List<UserCollection> userCollections = IndiegalaAccountClient.GetUserCollections();
            List<UserCollection> userCollections = new List<UserCollection>();
            return userCollections?.Find(x => x.id.ToString() == gameId)?.prod_slugged_name;
        }

        /*
        public List<GameMetadata> GetOwnedClient(IPlayniteAPI PlayniteApi)
        {
            List<GameMetadata> GamesOwnedClient = new List<GameMetadata>();

            if (IgCookies.Count == 0)
            {
                Logger.Warn($"GetOwnedClient() - No cookies");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    "Indiegala" + System.Environment.NewLine + PlayniteApi.Resources.GetString("LOCCommonLoginRequired"),
                    NotificationType.Error,
                    () =>
                    {
                        try
                        {
                            API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                        }
                        catch { }
                    }));
                return GamesOwnedClient;
            }

            string response = Web.DownloadStringData(ApiUrl, IgCookies, "galaClient").GetAwaiter().GetResult();

            if (!response.IsNullOrEmpty())
            {
                dynamic data = Serialization.FromJson<dynamic>(response);
                string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                List<UserCollection> userCollections = Serialization.FromJson<List<UserCollection>>(userCollectionString);

                foreach (UserCollection userCollection in userCollections)
                {
                    GamesOwnedClient.Add(new GameMetadata()
                    {
                        Source = new MetadataNameProperty("Indiegala"),
                        GameId = userCollection.id.ToString(),
                        Name = userCollection.prod_name,
                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                        LastActivity = null,
                        Playtime = 0,
                        Tags = userCollection.tags?.Select(x => new MetadataNameProperty(x.name)).Cast<MetadataProperty>().ToHashSet()
                    });
                }
            }

            return GamesOwnedClient;
        }
        */

        private List<GameMetadata> GetInstalledClient(List<GameMetadata> OwnedClient)
        {
            try
            {
                List<ClientInstalled> GamesInstalledInfo = IndiegalaClient.GetClientGameInstalled();

                foreach (GameMetadata gameMetadata in OwnedClient)
                {
                    UserCollection userCollection = IndiegalaClient.ClientData.data.showcase_content.content.user_collection.FirstOrDefault(x => x.id.ToString() == gameMetadata.GameId);

                    if (userCollection != null)
                    {
                        string SluggedName = userCollection.prod_slugged_name;
                        ClientInstalled clientInstalled = GamesInstalledInfo.FirstOrDefault(x => x.target.item_data.slugged_name == SluggedName);

                        if (clientInstalled != null)
                        {
                            List<GameAction> GameActions = null;

                            GameAction DownloadAction = null;
                            if (!clientInstalled.target.game_data.downloadable_win.IsNullOrEmpty())
                            {
                                DownloadAction = new GameAction()
                                {
                                    Name = "Download",
                                    Type = GameActionType.URL,
                                    Path = clientInstalled.target.game_data.downloadable_win
                                };

                                GameActions = new List<GameAction> { DownloadAction };
                            }


                            string GamePath = Path.Combine(clientInstalled.path[0], SluggedName);
                            string ExePath = string.Empty;
                            if (Directory.Exists(GamePath))
                            {
                                if (!clientInstalled.target.game_data.exe_path.IsNullOrEmpty())
                                {
                                    ExePath = clientInstalled.target.game_data.exe_path;
                                }
                                else
                                {
                                    Parallel.ForEach(Directory.EnumerateFiles(GamePath, "*.exe"),
                                        (objectFile) =>
                                        {
                                            if (!objectFile.Contains("UnityCrashHandler32.exe") && !objectFile.Contains("UnityCrashHandler64.exe"))
                                            {
                                                ExePath = Path.GetFileName(objectFile);
                                            }
                                        }
                                    );
                                }

                                GameAction PlayAction = new GameAction()
                                {
                                    Name = "Play",
                                    Type = GameActionType.File,
                                    Path = ExePath,
                                    WorkingDir = "{InstallDir}",
                                    IsPlayAction = true
                                };

                                if (GameActions != null)
                                {
                                    GameActions.Add(PlayAction);
                                }
                                else
                                {
                                    GameActions = new List<GameAction> { PlayAction };
                                }
                            }

                            ulong Playtime = (ulong)clientInstalled.playtime;


                            gameMetadata.InstallDirectory = GamePath;
                            gameMetadata.IsInstalled = true;
                            gameMetadata.Playtime = Playtime;
                            gameMetadata.GameActions = GameActions;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return OwnedClient;
        }
        #endregion


        private GameMetadata CheckIsInstalled(IndiegalaLibrarySettingsViewModel PluginSettings, GameMetadata gameMetadata)
        {
            bool IsInstalled = false;

            // Check with defined installation
            Game game = API.Instance.Database.Games.Where(x => x.GameId == gameMetadata.GameId)?.FirstOrDefault();
            if (game != null)
            {
                gameMetadata.IsInstalled = false;
                game.IsInstalled = false;

                List<GameAction> gameActions = game.GameActions?.Where(x => x.IsPlayAction)?.ToList();
                if (gameActions != null)
                {
                    foreach (GameAction gameAction in gameActions)
                    {
                        string PathPlayAction = Path.Combine
                        (
                            PlayniteTools.StringExpandWithoutStore(game, gameAction.WorkingDir) ?? string.Empty,
                            PlayniteTools.StringExpandWithoutStore(game, gameAction.Path)
                        );

                        if (File.Exists(PathPlayAction))
                        {
                            gameMetadata.IsInstalled = true;
                            game.IsInstalled = true;
                            IsInstalled = true;
                            break;
                        }
                    }
                }
            }

            if (!IsInstalled)
            {
                // Only if installed in client
                string InstallPathClient = string.Empty;
                if (PluginSettings.Settings.UseClient && IndiegalaClient.ClientData != null)
                {
                    InstallPathClient = IndiegalaClient.GameInstallPath;
                    UserCollection userCollection = IndiegalaClient.ClientData.data?.showcase_content?.content?.user_collection?.Find(x => x.id.ToString() == gameMetadata.GameId);
                    Common.LogDebug(true, Serialization.ToJson($"userCollection: {userCollection}"));
                    ClientGameInfo clientGameInfo = IndiegalaClient.GetClientGameInfo(gameMetadata.GameId);
                    Common.LogDebug(true, Serialization.ToJson($"clientGameInfo: {clientGameInfo}"));
                    
                    if (clientGameInfo != null && userCollection != null)
                    {
                        string PathDirectory = Path.Combine(InstallPathClient, userCollection.prod_slugged_name);
                        string ExeFile = clientGameInfo.exe_path ?? string.Empty;
                        if (ExeFile.IsNullOrEmpty() && Directory.Exists(PathDirectory))
                        {
                            SafeFileEnumerator fileEnumerator = new SafeFileEnumerator(PathDirectory, "*.exe", SearchOption.AllDirectories);
                            foreach (var file in fileEnumerator)
                            {
                                ExeFile = Path.GetFileName(file.FullName);
                            }
                        }

                        string PathFolder = Path.Combine(PathDirectory, ExeFile);
                        if (File.Exists(PathFolder))
                        {
                            gameMetadata.InstallDirectory = PathDirectory;
                            gameMetadata.IsInstalled = true;

                            if (gameMetadata.GameActions != null)
                            {
                                gameMetadata.GameActions.Add(new GameAction
                                {
                                    IsPlayAction = true,
                                    Name = Path.GetFileNameWithoutExtension(ExeFile),
                                    WorkingDir = "{InstallDir}",
                                    Path = ExeFile
                                });
                            }
                            else
                            {
                                List<GameAction> gameActions = new List<GameAction>();
                                gameActions.Add(new GameAction
                                {
                                    IsPlayAction = true,
                                    Name = Path.GetFileNameWithoutExtension(ExeFile),
                                    WorkingDir = "{InstallDir}",
                                    Path = ExeFile
                                });

                                gameMetadata.GameActions = gameActions;
                            }
                        }


                        if (game != null)
                        {
                            game.IsInstalled = gameMetadata.IsInstalled;
                            game.InstallDirectory = gameMetadata.InstallDirectory;
                            game.GameActions = gameMetadata.GameActions.ToObservable();
                        }
                    }
                }
            }

            if (game != null)
            {
                Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                {
                    API.Instance.Database.Games.Update(game);
                });
            }

            return gameMetadata;
        }


        public static GameMetadata GetMetadataWithClient(string Id)
        {
            if (IndiegalaClient.ClientData != null)
            {
                UserCollection userCollection = IndiegalaClient.ClientData.data.showcase_content.content.user_collection.Find(x => x.id.ToString() == Id);

                if (userCollection != null)
                {
                    ClientGameInfo clientGameInfo = IndiegalaClient.GetClientGameInfo(Id);

                    if (clientGameInfo != null)
                    {
                        int? CommunityScore = null;
                        if (clientGameInfo.rating.avg_rating != null)
                        {
                            CommunityScore = (int)clientGameInfo.rating.avg_rating * 20;
                        }

                        List<GameAction> GameActions = new List<GameAction>();
                        GameAction DownloadAction = null;
                        if (!clientGameInfo.downloadable_win.IsNullOrEmpty())
                        {
                            DownloadAction = new GameAction()
                            {
                                Name = "Download",
                                Type = GameActionType.URL,
                                Path = clientGameInfo.downloadable_win
                            };
                            GameActions = new List<GameAction> { DownloadAction };
                        }

                        GameMetadata gameMetadata = new GameMetadata()
                        {
                            Links = new List<Link>(),
                            Tags = clientGameInfo.tags?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            Genres = clientGameInfo.categories?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            Features = clientGameInfo.specs?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            GameActions = GameActions,
                            ReleaseDate = new ReleaseDate(userCollection.date),
                            CommunityScore = CommunityScore,
                            Description = clientGameInfo.description_long,
                            Developers = userCollection.prod_dev_username.IsEqual("galaFreebies") ? null : new HashSet<MetadataProperty> { new MetadataNameProperty(userCollection.prod_dev_username) }
                        };

                        if (!userCollection.prod_dev_cover.IsNullOrEmpty())
                        {
                            MetadataFile bg = new MetadataFile(string.Format(ProdCoverUrl, userCollection.prod_dev_namespace, userCollection.prod_id_key_name, userCollection.prod_dev_cover));
                            gameMetadata.BackgroundImage = bg;
                        }

                        if (!userCollection.prod_dev_image.IsNullOrEmpty())
                        {
                            MetadataFile c = new MetadataFile(string.Format(ProdMainUrl, userCollection.prod_dev_namespace, userCollection.prod_id_key_name, userCollection.prod_dev_image));
                            gameMetadata.CoverImage = c;
                        }

                        return gameMetadata;
                    }
                }
            }

            return null;
        }
    }
}
