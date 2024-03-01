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

namespace IndiegalaLibrary.Services
{
    internal enum DataType
    {
        bundle, store
    }


    public enum ConnectionState
    {
        Logged, Locked, Unlogged
    }


    public class IndiegalaAccountClient
    {
        private static ILogger Logger => LogManager.GetLogger();

        private static string BaseUrl => "https://www.indiegala.com";
        private string LoginUrl => $"{BaseUrl}/login";
        private string LogoutUrl => $"{BaseUrl}/logout";
        private string LibraryUrl => $"{BaseUrl}/library";
        private string ShowcaseUrl => BaseUrl + "/library/showcase/{0}";
        private string BundleUrl => BaseUrl + "/library/bundle/{0}";
        private string StoreUrl => BaseUrl + "/library/store/{0}";
        private static string StoreSearch => "https://www.indiegala.com/search/query";
        private static string ShowcaseSearch => "https://www.indiegala.com/showcase/ajax/{0}";

        private static string UrlGetStore => $"{BaseUrl}/library/get-store-contents";
        private static string UrlGetBundle => $"{BaseUrl}/library/get-bundle-contents";

        private static string ApiUrl => $"{BaseUrl}/login_new/user_info";

        private static string ProdCoverUrl => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodcover/{2}";
        private static string ProdMainUrl => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodmain/{2}";

        public bool IsConnected { get; set; } = false;
        public bool IsLocked { get; set; } = false;

        private static List<HttpCookie> _IgCookies = new List<HttpCookie>();
        private static List<HttpCookie> IgCookies
        {
            get
            {
                if (_IgCookies?.Count > 0)
                {
                    using (var WebViewOffscreen = API.Instance.WebViews.CreateOffscreenView())
                    {
                        List<HttpCookie> Cookies = WebViewOffscreen.GetCookies();
                        _IgCookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    }
                }
                return _IgCookies;
            }
            set => _IgCookies = value;
        }

        private static List<UserCollection> UserCollections { get; set; } = new List<UserCollection>();


        public IndiegalaAccountClient()
        {

        }


        public void LoginWithClient()
        {
            Logger.Info("LoginWithClient()");

            IsConnected = false;
            ResetClientCookies();

            IndieglaClient indieglaClient = new IndieglaClient();
            indieglaClient.Open();
        }

        public void LoginWithoutClient()
        {
            Logger.Info("LoginWithoutClient()");

            IsConnected = false;
            ResetClientCookies();

            using (IWebView WebViews = API.Instance.WebViews.CreateView(670, 670))
            {

                WebViews.LoadingChanged += (s, e) =>
                {
                    Common.LogDebug(true, $"NavigationChanged - {WebViews.GetCurrentAddress()}");

                    if (WebViews.GetCurrentAddress().IndexOf("https://www.indiegala.com/") > -1 && WebViews.GetCurrentAddress().IndexOf(LoginUrl) == -1 && WebViews.GetCurrentAddress().IndexOf(LogoutUrl) == -1)
                    {
                        Common.LogDebug(true, $"_webView.Close();");
                        IsConnected = true;
                        WebViews.Close();
                    }
                };

                IsConnected = false;
                WebViews.Navigate(LogoutUrl);
                _ = WebViews.OpenDialog();
            }
        }


        public void ResetClientCookies()
        {
            IgCookies = new List<HttpCookie>();
        }


        // TODO Not used for the moment
        // TODO Must be rewrite
        public bool GetIsUserLoggedInWithClient()
        {
            string WebData = Web.DownloadStringData(LibraryUrl, IgCookies).GetAwaiter().GetResult();

            IsLocked = WebData.Contains("profile locked", StringComparison.CurrentCultureIgnoreCase);
            IsConnected = WebData.Contains("private-body");

            if (!IsConnected)
            {
                Logger.Warn("User is not connected with client");
                return false;
            }
            else
            {
                Logger.Info("User is connected with client");
                return true;
            }
        }

        public ConnectionState GetIsUserLoggedInWithoutClient()
        {
            using (IWebView webView = API.Instance.WebViews.CreateOffscreenView())
            {
                webView.NavigateAndWait(LoginUrl);
                IsLocked = webView.GetPageSource().Contains("profile locked", StringComparison.CurrentCultureIgnoreCase);
                if (IsLocked)
                {
                    Logger.Warn("The profil is locked");
                    return ConnectionState.Locked;
                }

                if (webView.GetCurrentAddress().StartsWith(LoginUrl))
                {
                    Logger.Warn("User is not connected without client");
                    IsConnected = false;
                    return ConnectionState.Unlogged;
                }


                IgCookies = webView.GetCookies().Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();

                if (IgCookies?.Count > 0)
                {
                    Common.LogDebug(true, Serialization.ToJson(IgCookies));

                    Logger.Info("User is connected without client");
                    IsConnected = true;

                    return ConnectionState.Logged;
                }
                else
                {
                    Logger.Info("User is not connected without client (no cookies)");
                    IsConnected = false;

                    return ConnectionState.Unlogged;
                }
            }
        }


        #region SearchData
        public static List<ResultResponse> SearchGame(IPlayniteAPI PlayniteApi, string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            List<ResultResponse> ResultStore = SearchGameStore(PlayniteApi, GameName);
            List<ResultResponse> ResultShowcase = SearchGameShowcase(PlayniteApi, GameName);

            Result = Result.Concat(ResultStore).Concat(ResultShowcase).ToList();
            Common.LogDebug(true, $"Result: {Serialization.ToJson(Result)}");

            return Result;
        }

        public static List<ResultResponse> SearchGameStore(IPlayniteAPI PlayniteApi, string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            string payload = "{\"input_string\": \"" + GameName + "\"}";
            try
            {
                var Cookies = PlayniteApi.WebViews.CreateOffscreenView().GetCookies();
                Cookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                Common.LogDebug(true, Serialization.ToJson(Cookies));

                if (Cookies.Count == 0)
                {
                    Logger.Warn($"SearchGameStore() - No cookies");
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
                    return Result;
                }

                string WebResult = Web.PostStringDataPayload(StoreSearch, payload, Cookies).GetAwaiter().GetResult().Replace(Environment.NewLine, string.Empty);
                SearchResponse searchResponse = NormalizeResponseSearch(WebResult);

                if (searchResponse != null && !searchResponse.Html.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument htmlDocument = parser.Parse(searchResponse.Html.Replace("\\", string.Empty));

                    foreach (var liElement in htmlDocument.QuerySelectorAll("ul.result-section li"))
                    {
                        if (liElement.GetAttribute("class").IsNullOrEmpty() || (!liElement.GetAttribute("class").Contains("results-top") && !liElement.GetAttribute("class").Contains("view-more")))
                        {
                            var figure = liElement.QuerySelector("figure");
                            var title = liElement.QuerySelector("div.title");

                            if (figure != null && title != null)
                            {
                                Result.Add(new ResultResponse
                                {
                                    Name = WebUtility.HtmlDecode(title.QuerySelector("a").InnerHtml.Replace("<span class=\"search-match\">", string.Empty).Replace("</span>", string.Empty)),
                                    ImageUrl = figure.QuerySelector("img").GetAttribute("src"),
                                    StoreUrl = BaseUrl + figure.QuerySelector("a").GetAttribute("href")
                                });
                            }
                        }
                    }
                }
                else
                {
                    Logger.Warn($"No game store search");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return Result;
        }

        public static List<ResultResponse> SearchGameShowcase(IPlayniteAPI PlayniteApi, string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            try
            {
                var Cookies = PlayniteApi.WebViews.CreateOffscreenView().GetCookies();
                Cookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                Common.LogDebug(true, Serialization.ToJson(Cookies));

                if (Cookies.Count == 0)
                {
                    Logger.Warn($"SearchGameShowcase() - No cookies");
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
                    return Result;
                }

                int n = 1;
                string WebResult = string.Empty;
                string url = string.Empty;
                bool isGood = false;
                while (!isGood)
                {
                    url = string.Format(ShowcaseSearch, n.ToString());
                    Logger.Info($"Search on {url}");
                    try
                    {
                        WebResult = Web.DownloadStringData(url, Cookies).GetAwaiter().GetResult();

                        if (WebResult.ToLower().Contains("no results found"))
                        {
                            isGood = true;
                        }
                        else if (!WebResult.IsNullOrEmpty())
                        {
                            SearchResponse searchResponse = NormalizeResponseSearch(WebResult);

                            if (searchResponse != null && !searchResponse.Html.IsNullOrEmpty())
                            {
                                HtmlParser parser = new HtmlParser();
                                IHtmlDocument htmlDocument = parser.Parse(searchResponse.Html.Replace("\\", string.Empty));

                                foreach (var liElement in htmlDocument.QuerySelectorAll("div.main-list-item-col"))
                                {
                                    try
                                    {
                                        string Name = WebUtility.HtmlDecode(liElement.QuerySelector("div.showcase-title").InnerHtml.Replace("<span class=\"search-match\">", string.Empty).Replace("</span>", string.Empty));
                                        string ImageUrl = liElement.QuerySelector("img.img-fit").GetAttribute("data-img-src");
                                        string StoreUrl = liElement.QuerySelector("a.main-list-item-clicker").GetAttribute("href");

                                        if (Name.ToLower().Contains(GameName.ToLower()))
                                        {
                                            Result.Add(new ResultResponse
                                            {
                                                Name = Name,
                                                ImageUrl = ImageUrl,
                                                StoreUrl = StoreUrl
                                            });
                                        }
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                            else
                            {
                                Logger.Warn($"Not more showcase search");
                                isGood = true;
                            }
                        }
                        else
                        {
                            Logger.Warn($"Not find showcase search");
                            isGood = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, "Error in download search");
                        isGood = true;
                    }

                    n++;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on SearchGameShowcase()");
            }

            return Result;
        }


        private static SearchResponse NormalizeResponseSearch(string ResponseSearch)
        {
            ResponseSearch = ResponseSearch.Replace(Environment.NewLine, string.Empty);
            ResponseSearch = Regex.Replace(ResponseSearch, @"\r\n?|\n", string.Empty);

            string start = ResponseSearch.Substring(0, ResponseSearch.IndexOf("\"html\": \"") + 9);
            string end = "\"}";

            ResponseSearch = ResponseSearch.Replace(start, string.Empty).Replace(end, string.Empty);
            ResponseSearch = ResponseSearch.Replace("\"", "\\\"").Replace("\\\\", "\\");

            ResponseSearch = start + ResponseSearch.Replace("\"", "\\\"").Replace("\\\\", "\\") + end;

            Common.LogDebug(true, $"ResponseSearch: {ResponseSearch}");

            SearchResponse searchResponse = new SearchResponse();
            try
            {
                searchResponse = Serialization.FromJson<SearchResponse>(ResponseSearch);
            }
            catch
            {

            }

            Common.LogDebug(true, $"searchResponse: {Serialization.ToJson(searchResponse)}");

            return searchResponse;
        }
        #endregion


        public static List<UserCollection> GetUserCollections()
        {
            if (IndiegalaAccountClient.UserCollections?.Count > 0)
            {
                return IndiegalaAccountClient.UserCollections;
            }

            try
            {
                using (IWebView WebViews = API.Instance.WebViews.CreateOffscreenView())
                {
                    List<HttpCookie> Cookies = WebViews.GetCookies();
                    Cookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    Common.LogDebug(true, Serialization.ToJson(Cookies));

                    if (Cookies.Count == 0)
                    {
                        Logger.Warn($"GetUserCollections() - No cookies");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            "Indiegala-Error-UserCollections",
                            "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCCommonLoginRequired"),
                            NotificationType.Error,
                            () =>
                            {
                                try
                                {
                                    API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                                }
                                catch { }
                            }));
                        return new List<UserCollection>();
                    }

                    string response = Web.DownloadStringData(ApiUrl, Cookies, "galaClient").GetAwaiter().GetResult();

                    if (!response.IsNullOrEmpty())
                    {
                        dynamic data = Serialization.FromJson<dynamic>(response);
                        string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                        List<UserCollection> userCollections = Serialization.FromJson<List<UserCollection>>(userCollectionString);

                        IndiegalaAccountClient.UserCollections = userCollections;
                        return IndiegalaAccountClient.UserCollections;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
                API.Instance.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCCommonLoginRequired"),
                    NotificationType.Error,
                    () =>
                    {
                        try
                        {
                            API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                        }
                        catch { }
                    }));
            }

            return new List<UserCollection>();
        }


        public List<GameMetadata> GetOwnedGames(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings)
        {
            List<GameMetadata> OwnedGames = new List<GameMetadata>();

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            List<GameMetadata> OwnedGamesShowcase = new List<GameMetadata>();
            OwnedGamesShowcase = GetOwnedGamesShowcase(Plugin, PluginSettings);

            var dataGames = IndiegalaApi.GetUserCollections();
            dataGames?.ForEach(x =>
            {
                var data = IndiegalaApi.GetClientGameDetails(x);
            });



            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            Logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            stopWatch.Reset();
            stopWatch.Start();

            List<GameMetadata> OwnedGamesBundle = new List<GameMetadata>();
            OwnedGamesBundle = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.bundle);

            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            Logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            stopWatch.Reset();
            stopWatch.Start();

            List<GameMetadata> OwnedGamesStore = new List<GameMetadata>();
            OwnedGamesStore = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.store);

            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            Logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            OwnedGames = OwnedGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();
            Common.LogDebug(true, $"OwnedGames: {Serialization.ToJson(OwnedGames)}");

            return OwnedGames;
        }


        #region Client
        public static string GetProdSluggedName(string GameId)
        {
            List<UserCollection> userCollections = IndiegalaAccountClient.GetUserCollections();
            return userCollections?.Find(x => x.id.ToString() == GameId)?.prod_slugged_name;
        }

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

        private List<GameMetadata> GetInstalledClient(List<GameMetadata> OwnedClient)
        {
            try
            {
                List<ClientInstalled> GamesInstalledInfo = IndieglaClient.GetClientGameInstalled();

                foreach (GameMetadata gameMetadata in OwnedClient)
                {
                    UserCollection userCollection = IndieglaClient.ClientData.data.showcase_content.content.user_collection.Where(x => x.id.ToString() == gameMetadata.GameId).FirstOrDefault();

                    if (userCollection != null)
                    {
                        string SluggedName = userCollection.prod_slugged_name;
                        ClientInstalled clientInstalled = GamesInstalledInfo.Where(x => x.target.item_data.slugged_name == SluggedName).FirstOrDefault();

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


        private List<GameMetadata> GetOwnedGamesBundleStore(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings, DataType dataType)
        {
            List<GameMetadata> OwnedGames = new List<GameMetadata>();


            string originData = string.Empty;
            string originUrl = string.Empty;
            switch (dataType)
            {
                case DataType.bundle:
                    originData = "bundle";
                    originUrl = BundleUrl;
                    break;

                case DataType.store:
                    originData = "store";
                    originUrl = StoreUrl;
                    break;
            }


            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(originUrl, n.ToString());
                Logger.Info($"Get on {url}");
                try
                {
                    ResultWeb = Web.DownloadStringData(url, IgCookies).GetAwaiter().GetResult();
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        IElement DataElement = htmlDocument.QuerySelector($"div.profile-private-page-library-tab-{originData}");
                        if (DataElement != null)
                        {
                            // End list ?
                            IElement noElement = DataElement.QuerySelector("div.profile-private-page-library-no-results");
                            if (noElement != null)
                            {
                                Logger.Info($"End list");
                                isGood = true;
                                return OwnedGames;
                            }


                            foreach (IElement elList in DataElement.QuerySelectorAll("ul.profile-private-page-library-list li"))
                            {
                                string aAttribute = elList.QuerySelector("a")?.GetAttribute("onclick");
                                MatchCollection Matches = Regex.Matches(aAttribute, @"\'(.*?)\'", RegexOptions.IgnoreCase);

                                string id = string.Empty;
                                string urlData = string.Empty;
                                string payload = string.Empty;
                                switch (dataType)
                                {
                                    case DataType.bundle:
                                        //showStoreContents('5088849753145344', this, event)
                                        id = Matches[1].Value.Replace("'", string.Empty);
                                        payload = "{\"version\":\"" + id + "\"}";
                                        urlData = UrlGetBundle;
                                        break;

                                    case DataType.store:
                                        //onclick="showBundleContents('bundle20201023', '20201023', this, event)
                                        id = Matches[0].Value.Replace("'", string.Empty);
                                        payload = "{\"cart_id\":\"" + id + "\"}";
                                        urlData = UrlGetStore;
                                        break;
                                }

                                if (IgCookies.Count == 0)
                                {
                                    Logger.Warn($"GetOwnedGamesBundleStore() - No cookies");
                                    Plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                                        "Indiegala-Error-UserCollections",
                                        "Indiegala" + System.Environment.NewLine + Plugin.PlayniteApi.Resources.GetString("LOCCommonLoginRequired"),
                                        NotificationType.Error,
                                        () =>
                                        {
                                            try
                                            {
                                                API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                                            }
                                            catch { }
                                        }));
                                    return OwnedGames;
                                }

                                string response = Web.PostStringDataPayload(urlData, payload, IgCookies).GetAwaiter().GetResult();
                                StoreBundleResponse storeBundleResponse = ParseBundleResponse(response);


                                if (storeBundleResponse.status != "ok")
                                {
                                    Logger.Warn($"No data for {originData} - {id}");
                                    continue;
                                }

                                parser = new HtmlParser();
                                htmlDocument = parser.Parse(storeBundleResponse.html);

                                List<BundleGameData> gameBundleOrOrderData = GetStoreGameData(storeBundleResponse.html).ToList();

                                foreach (var game in gameBundleOrOrderData)
                                {
                                    if (game.IsKey)
                                    {
                                        Logger.Info($"{game.Name} is not a Indiegala game in {originData}");
                                        continue;
                                    }

                                    GameMetadata tempGameInfo = new GameMetadata()
                                    {
                                        Source = new MetadataNameProperty("Indiegala"),
                                        GameId = game.Name.GetSHA256Hash(),
                                        Name = game.Name,
                                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                                        GameActions = new List<GameAction>(),
                                        Links = new List<Link>(),
                                    };

                                    if (!game.StoreUrl.IsNullOrEmpty())
                                        tempGameInfo.Links.Add(new Link("Store", game.StoreUrl));

                                    if (!game.DownloadUrl.IsNullOrEmpty())
                                        tempGameInfo.GameActions.Add(new GameAction
                                        {
                                            Name = "Download",
                                            Type = GameActionType.URL,
                                            Path = game.DownloadUrl,
                                        });

                                    try
                                    {
                                        tempGameInfo = CheckIsInstalled(PluginSettings, tempGameInfo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Common.LogError(ex, false);
                                    }

                                    Common.LogDebug(true, $"Find {Serialization.ToJson(tempGameInfo)}");
                                    Common.LogDebug(true, $"Find {originData} - {tempGameInfo.GameId} {tempGameInfo.Name}");
                                    OwnedGames.Add(tempGameInfo);
                                }
                            }
                        }
                        else
                        {
                            Logger.Warn($"No {originData} data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        Logger.Warn($"Not find {originData}");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error in download library");
                    isGood = true;
                    return OwnedGames;
                }

                n++;
            }

            return OwnedGames;
        }

        private static Regex BundleResponseRegex = new Regex(
            @"^\{\s*""status"":\s*""(?<status>\w+)"",\s*""code"":\s*""(?<code>\w*)"",\s*""html"":\s*""(?<html>.*)""}$",
            RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        /// <summary>
        /// The JSON deserializer often chokes on random unescaped quotes or line breaks in the html field, so this attempts to parse it with a regex first.
        /// It's not nice, but then neither is the JSON that IndieGala returns.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private static StoreBundleResponse ParseBundleResponse(string content)
        {
            Match match = BundleResponseRegex.Match(content);
            if (match.Success)
                return new StoreBundleResponse
                {
                    code = match.Groups["code"].Value,
                    status = match.Groups["status"].Value,
                    html = match.Groups["html"].Value.Replace("\\\"", "\"")
                };

            return Serialization.FromJson<StoreBundleResponse>(content);
        }

        private class BundleGameData
        {
            public string Name;
            public string StoreUrl;
            public string DownloadUrl;
            public bool IsKey;
        }

        private static IEnumerable<BundleGameData> GetStoreGameData(string html)
        {
            HtmlParser parser = new HtmlParser();
            IHtmlDocument htmlDocument = parser.Parse(html);

            foreach (IElement listItem in htmlDocument.QuerySelectorAll("li.profile-private-page-library-subitem"))
            {
                Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                BundleGameData data = ParseGameDataListItemNew(listItem) ?? ParseGameDataListItemOld(listItem);

                if (data != null)
                {
                    data.StoreUrl = listItem.QuerySelector("figure a")?.GetAttribute("href");
                    data.IsKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial") != null;
                    yield return data;
                }
            }
        }

        private static BundleGameData ParseGameDataListItemOld(AngleSharp.Dom.IElement listItem)
        {
            if (listItem.QuerySelector("i").ClassList.Where(x => x.Contains("fa-windows"))?.Count() == 0)
            {
                return null;
            }

            string name = listItem.QuerySelector("figcaption div.profile-private-page-library-title div")?.InnerHtml;
            if (name.IsNullOrEmpty())
            {
                Logger.Error($"No Name in {listItem.InnerHtml} (method 1)");
                return null;
            }

            string downloadUrl = listItem.QuerySelector("figcaption a.bg-gradient-light-blue")?.GetAttribute("href");
            if (downloadUrl.IsNullOrEmpty())
            {
                Logger.Warn($"UrlDownload not found for {name} (method 1)");
            }

            return new BundleGameData { Name = name, DownloadUrl = downloadUrl };
        }

        private static BundleGameData ParseGameDataListItemNew(AngleSharp.Dom.IElement listItem)
        {
            string name = listItem.QuerySelector("figcaption div.profile-private-page-library-title div")?.InnerHtml;
            string downloadUrl = listItem.QuerySelectorAll("a").FirstOrDefault(a => a.InnerHtml.Trim() == "Download")?.GetAttribute("href");
            if (name.IsNullOrEmpty() || downloadUrl.IsNullOrEmpty())
            {
                Logger.Error($"No name or download URL in {listItem.InnerHtml} (method 2)");
                return null;
            }

            return new BundleGameData { Name = name, DownloadUrl = downloadUrl };
        }

        private List<GameMetadata> GetOwnedGamesShowcase(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings)
        {
            List<GameMetadata> OwnedGames = new List<GameMetadata>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(ShowcaseUrl, n.ToString());
                Logger.Info($"Get on {url}");
                try
                {
                    ResultWeb = Web.DownloadStringData(url, IgCookies).GetAwaiter().GetResult();
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        // Showcase
                        IElement ShowcaseElement = htmlDocument.QuerySelector("div.profile-private-page-library-tab-showcase");
                        if (ShowcaseElement != null)
                        {
                            // End list ?
                            IElement noElement = ShowcaseElement.QuerySelector("div.profile-private-page-library-no-results");
                            if (noElement != null)
                            {
                                Logger.Info($"End list");
                                isGood = true;
                                return OwnedGames;
                            }

                            foreach (IElement SearchElement in ShowcaseElement.QuerySelectorAll("ul.profile-private-page-library-sublist"))
                            {
                                IElement Element = SearchElement.QuerySelector("div.profile-private-page-library-subitem");
                                string GameId = Element?.GetAttribute("id").Replace("showcase-item-", string.Empty);
                                if (GameId.IsNullOrEmpty())
                                {
                                    Logger.Error($"IndiegalaLibrary - No GameId in {Element.InnerHtml}");
                                    continue;
                                }

                                Element = SearchElement.QuerySelector("div.profile-private-showcase-sub-section-row-cont");

                                List<Link> StoreLink = new List<Link>();
                                IElement tempLink = Element.QuerySelector("a");
                                if (tempLink != null)
                                {
                                    StoreLink.Add(new Link("Store", tempLink.GetAttribute("href")));
                                }

                                string Name = SearchElement.QuerySelector("a.library-showcase-title")?.InnerHtml;
                                if (Name.IsNullOrEmpty())
                                {
                                    Logger.Error($"No Name in {Element.InnerHtml}");
                                    continue;
                                }

                                string UrlDownload = string.Empty;
                                GameAction DownloadAction = new GameAction();
                                List<GameAction> GameActions = new List<GameAction>();

                                UrlDownload = SearchElement.QuerySelector("a.library-showcase-download-btn")?.GetAttribute("onclick");
                                if (!UrlDownload.IsNullOrEmpty())
                                {
                                    UrlDownload = UrlDownload.Replace("location.href='", string.Empty);
                                    UrlDownload = UrlDownload.Substring(0, UrlDownload.Length - 1);
                                    DownloadAction = new GameAction()
                                    {
                                        Name = "Download",
                                        Type = GameActionType.URL,
                                        Path = UrlDownload
                                    };

                                    GameActions = new List<GameAction> { DownloadAction };
                                }
                                else
                                {
                                    Logger.Warn($"UrlDownload not found for {Name}");
                                }

                                Common.LogDebug(true, $"Find showcase - {GameId} {Name}");

                                GameMetadata gameInfo = new GameMetadata()
                                {
                                    Source = new MetadataNameProperty("Indiegala"),
                                    GameId = GameId,
                                    Name = Name,
                                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                                    GameActions = GameActions,
                                    Links = StoreLink
                                };

                                try
                                {
                                    gameInfo = CheckIsInstalled(PluginSettings, gameInfo);
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false);
                                }

                                OwnedGames.Add(gameInfo);
                            }
                        }
                        else
                        {
                            Logger.Warn($"No showcase data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        Logger.Warn($"Not find showcase");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error in download library");
                    isGood = true;
                    return OwnedGames;
                }

                n++;
            }

            return OwnedGames;
        }

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
                if (PluginSettings.Settings.UseClient && IndieglaClient.ClientData != null)
                {
                    InstallPathClient = IndieglaClient.GameInstallPath;
                    UserCollection userCollection = IndieglaClient.ClientData.data?.showcase_content?.content?.user_collection?.Find(x => x.id.ToString() == gameMetadata.GameId);
                    Common.LogDebug(true, Serialization.ToJson($"userCollection: {userCollection}"));
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(gameMetadata.GameId);
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
            if (IndieglaClient.ClientData != null)
            {
                UserCollection userCollection = IndieglaClient.ClientData.data.showcase_content.content.user_collection.Find(x => x.id.ToString() == Id);

                if (userCollection != null)
                {
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(Id);

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
