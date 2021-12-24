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
        private static ILogger logger = LogManager.GetLogger();

        private const string baseUrl = "https://www.indiegala.com";
        public string loginUrl = $"{baseUrl}/login";
        public string logoutUrl = $"{baseUrl}/logout";
        private string libraryUrl = $"{baseUrl}/library";
        private string showcaseUrl = baseUrl + "/library/showcase/{0}";
        private string bundleUrl = baseUrl + "/library/bundle/{0}";
        private string storeUrl = baseUrl + "/library/store/{0}";
        private static string storeSearch = "https://www.indiegala.com/search/query";
        private static string showcaseSearch = "https://www.indiegala.com/showcase/ajax/{0}";

        private static string urlGetStore = $"{baseUrl}/library/get-store-contents";
        private static string urlGetBundle = $"{baseUrl}/library/get-bundle-contents";

        private static string apiUrl = $"{baseUrl}/login_new/user_info";

        private const string ProdCoverUrl = "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodcover/{2}";

        public bool isConnected = false;
        public bool isLocked = false;

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
            set
            {
                _IgCookies = value;
            }
        }

        private static List<UserCollection> userCollections = new List<UserCollection>();


        public IndiegalaAccountClient()
        {

        }


        public void LoginWithClient()
        {
            logger.Info("LoginWithClient()");

            isConnected = false;
            ResetClientCookies();

            IndieglaClient indieglaClient = new IndieglaClient();
            indieglaClient.Open();
        }

        public void LoginWithoutClient()
        {
            logger.Info("LoginWithoutClient()");

            isConnected = false;
            ResetClientCookies();

            using (IWebView WebViews = API.Instance.WebViews.CreateView(670, 670))
            {

                WebViews.LoadingChanged += (s, e) =>
                {
                    Common.LogDebug(true, $"NavigationChanged - {WebViews.GetCurrentAddress()}");

                    if (WebViews.GetCurrentAddress().IndexOf("https://www.indiegala.com/") > -1 && WebViews.GetCurrentAddress().IndexOf(loginUrl) == -1 && WebViews.GetCurrentAddress().IndexOf(logoutUrl) == -1)
                    {
                        Common.LogDebug(true, $"_webView.Close();");
                        isConnected = true;
                        WebViews.Close();
                    }
                };

                isConnected = false;
                WebViews.Navigate(logoutUrl);
                WebViews.OpenDialog();
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
            string WebData = Web.DownloadStringData(libraryUrl, IgCookies).GetAwaiter().GetResult();

            isLocked = WebData.Contains("profile locked", StringComparison.CurrentCultureIgnoreCase);
            isConnected = WebData.Contains("private-body");

            if (!isConnected)
            {
                logger.Warn("User is not connected with client");
                return false;
            }
            else
            {
                logger.Info("User is connected with client");
                return true;
            }
        }

        public ConnectionState GetIsUserLoggedInWithoutClient()
        {
            using (var webView = API.Instance.WebViews.CreateOffscreenView())
            {
                webView.NavigateAndWait(loginUrl);
                isLocked = webView.GetPageSource().Contains("profile locked", StringComparison.CurrentCultureIgnoreCase);
                if (isLocked)
                {
                    logger.Warn("The profil is locked");
                    return ConnectionState.Locked;
                }

                if (webView.GetCurrentAddress().StartsWith(loginUrl))
                {
                    logger.Warn("User is not connected without client");
                    isConnected = false;
                    return ConnectionState.Unlogged;
                }


                IgCookies = webView.GetCookies().Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();

                if (IgCookies?.Count > 0)
                {
                    Common.LogDebug(true, Serialization.ToJson(IgCookies));

                    logger.Info("User is connected without client");
                    isConnected = true;

                    return ConnectionState.Logged;
                }
                else
                {
                    logger.Info("User is not connected without client (no cookies)");
                    isConnected = false;

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
                    logger.Warn($"SearchGameStore() - No cookies");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "Indiegala-Error-UserCollections",
                        "Indiegala" + System.Environment.NewLine + PlayniteApi.Resources.GetString("LOCLoginRequired"),
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

                string WebResult = Web.PostStringDataPayload(storeSearch, payload, Cookies).GetAwaiter().GetResult().Replace(Environment.NewLine, string.Empty);
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

                            try
                            {
                                Result.Add(new ResultResponse
                                {
                                    Name = WebUtility.HtmlDecode(title.QuerySelector("a").InnerHtml.Replace("<span class=\"search-match\">", string.Empty).Replace("</span>", string.Empty)),
                                    ImageUrl = figure.QuerySelector("img").GetAttribute("src"),
                                    StoreUrl = baseUrl + figure.QuerySelector("a").GetAttribute("href")
                                });
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                else
                {
                    logger.Warn($"No game store search");
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
                    logger.Warn($"SearchGameShowcase() - No cookies");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "Indiegala-Error-UserCollections",
                        "Indiegala" + System.Environment.NewLine + PlayniteApi.Resources.GetString("LOCLoginRequired"),
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
                    url = string.Format(showcaseSearch, n.ToString());
                    logger.Info($"Search on {url}");
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
                                logger.Warn($"Not more showcase search");
                                isGood = true;
                            }
                        }
                        else
                        {
                            logger.Warn($"Not find showcase search");
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
            if (IndiegalaAccountClient.userCollections?.Count > 0)
            {
                return IndiegalaAccountClient.userCollections;
            }

            try
            {
                using (var WebViews = API.Instance.WebViews.CreateOffscreenView())
                {
                    List<HttpCookie> Cookies = WebViews.GetCookies();
                    Cookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    Common.LogDebug(true, Serialization.ToJson(Cookies));

                    if (Cookies.Count == 0)
                    {
                        logger.Warn($"GetUserCollections() - No cookies");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            "Indiegala-Error-UserCollections",
                            "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCLoginRequired"),
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

                    string response = Web.DownloadStringData(apiUrl, Cookies, "galaClient").GetAwaiter().GetResult();

                    if (!response.IsNullOrEmpty())
                    {
                        dynamic data = Serialization.FromJson<dynamic>(response);
                        string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                        List<UserCollection> userCollections = Serialization.FromJson<List<UserCollection>>(userCollectionString);

                        IndiegalaAccountClient.userCollections = userCollections;
                        return IndiegalaAccountClient.userCollections;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
                API.Instance.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCLoginRequired"),
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

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            stopWatch.Reset();
            stopWatch.Start();

            List<GameMetadata> OwnedGamesBundle = new List<GameMetadata>();
            OwnedGamesBundle = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.bundle);

            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            stopWatch.Reset();
            stopWatch.Start();

            List<GameMetadata> OwnedGamesStore = new List<GameMetadata>();
            OwnedGamesStore = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.store);

            stopWatch.Stop();
            ts = stopWatch.Elapsed;
            logger.Info($"GetOwnedGamesShowcase - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");


            OwnedGames = OwnedGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();
            Common.LogDebug(true, $"OwnedGames: {Serialization.ToJson(OwnedGames)}");

            return OwnedGames;
        }


        #region Client
        public static string GetProdSluggedName(IPlayniteAPI PlayniteApi, string GameId)
        {
            List<UserCollection> userCollections = IndiegalaAccountClient.GetUserCollections();
            return userCollections?.Find(x => x.id.ToString() == GameId)?.prod_slugged_name;
        }

        public List<GameMetadata> GetOwnedClient(IPlayniteAPI PlayniteApi)
        {
            List<GameMetadata> GamesOwnedClient = new List<GameMetadata>();

            if (IgCookies.Count == 0)
            {
                logger.Warn($"GetOwnedClient() - No cookies");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    "Indiegala" + System.Environment.NewLine + PlayniteApi.Resources.GetString("LOCLoginRequired"),
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

            string response = Web.DownloadStringData(apiUrl, IgCookies, "galaClient").GetAwaiter().GetResult();

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
            var OwnedGames = new List<GameMetadata>();


            string originData = string.Empty;
            string originUrl = string.Empty;
            switch (dataType)
            {
                case DataType.bundle:
                    originData = "bundle";
                    originUrl = bundleUrl;
                    break;

                case DataType.store:
                    originData = "store";
                    originUrl = storeUrl;
                    break;
            }


            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(originUrl, n.ToString());
                logger.Info($"Get on {url}");
                try
                {
                    ResultWeb = Web.DownloadStringData(url, IgCookies).GetAwaiter().GetResult();
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        var DataElement = htmlDocument.QuerySelector($"div.profile-private-page-library-tab-{originData}");
                        if (DataElement != null)
                        {
                            // End list ?
                            var noElement = DataElement.QuerySelector("div.profile-private-page-library-no-results");
                            if (noElement != null)
                            {
                                logger.Info($"End list");
                                isGood = true;
                                return OwnedGames;
                            }


                            foreach (var elList in DataElement.QuerySelectorAll("ul.profile-private-page-library-list li"))
                            {
                                string aAttribute = elList.QuerySelector("a")?.GetAttribute("onclick");
                                var Matches = Regex.Matches(aAttribute, @"\'(.*?)\'", RegexOptions.IgnoreCase);

                                string id = string.Empty;
                                string urlData = string.Empty;
                                string payload = string.Empty;
                                switch (dataType)
                                {
                                    case DataType.bundle:
                                        //showStoreContents('5088849753145344', this, event)
                                        id = Matches[1].Value.Replace("'", string.Empty);
                                        payload = "{\"version\":\"" + id + "\"}";
                                        urlData = urlGetBundle;
                                        break;

                                    case DataType.store:
                                        //onclick="showBundleContents('bundle20201023', '20201023', this, event)
                                        id = Matches[0].Value.Replace("'", string.Empty);
                                        payload = "{\"cart_id\":\"" + id + "\"}";
                                        urlData = urlGetStore;
                                        break;
                                }

                                if (IgCookies.Count == 0)
                                {
                                    logger.Warn($"GetOwnedGamesBundleStore() - No cookies");
                                    Plugin.PlayniteApi.Notifications.Add(new NotificationMessage(
                                        "Indiegala-Error-UserCollections",
                                        "Indiegala" + System.Environment.NewLine + Plugin.PlayniteApi.Resources.GetString("LOCLoginRequired"),
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
                                StoreBundleResponse storeBundleResponse = Serialization.FromJson<StoreBundleResponse>(response);


                                if (storeBundleResponse.status != "ok")
                                {
                                    logger.Warn($"No data for {originData} - {id}");
                                    continue;
                                }

                                parser = new HtmlParser();
                                htmlDocument = parser.Parse(storeBundleResponse.html);

                                foreach (var listItem in htmlDocument.QuerySelectorAll("li.profile-private-page-library-subitem"))
                                {
                                    Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                                    if (listItem.QuerySelector("i").ClassList.Where(x => x.Contains("fa-windows"))?.Count() == 0)
                                    {
                                        continue;
                                    }

                                    string GameId = string.Empty;
                                    string Name = string.Empty;
                                    var GameActions = new List<GameAction>();
                                    List<Link> StoreLink = new List<Link>();

                                    Name = listItem.QuerySelector("figcaption div.profile-private-page-library-title div")?.InnerHtml;
                                    if (Name.IsNullOrEmpty())
                                    {
                                        logger.Error($"No Name in {listItem.InnerHtml}");
                                        continue;
                                    }

                                    GameId = Name.GetSHA256Hash();

                                    var tempLink = listItem.QuerySelector("figure a");
                                    if (tempLink != null)
                                    {
                                        StoreLink.Add(new Link("Store", tempLink.GetAttribute("href")));
                                    }

                                    var UrlDownload = listItem.QuerySelector("figcaption a.bg-gradient-light-blue")?.GetAttribute("href");
                                    if (!UrlDownload.IsNullOrEmpty())
                                    {
                                        GameAction DownloadAction = new GameAction()
                                        {
                                            Name = "Download",
                                            Type = GameActionType.URL,
                                            Path = UrlDownload
                                        };

                                        GameActions = new List<GameAction> { DownloadAction };
                                    }
                                    else
                                    {
                                        logger.Warn($"UrlDownload not found for {Name}");
                                    }

                                    var tempGameInfo = new GameMetadata()
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
                                        tempGameInfo = CheckIsInstalled(Plugin, PluginSettings, tempGameInfo);
                                    }
                                    catch (Exception ex)
                                    {
                                        Common.LogError(ex, false);
                                    }

                                    Common.LogDebug(true, $"Find {Serialization.ToJson(tempGameInfo)}");

                                    var HaveKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial");
                                    if (HaveKey == null)
                                    {
                                        Common.LogDebug(true, $"Find {originData} - {GameId} {Name}");
                                        OwnedGames.Add(tempGameInfo);
                                    }
                                    else
                                    {
                                        logger.Info($"Is not a Indiegala game in {originData} - {GameId} {Name}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            logger.Warn($"No {originData} data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"Not find {originData}");
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

        private List<GameMetadata> GetOwnedGamesShowcase(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings)
        {
            var OwnedGames = new List<GameMetadata>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(showcaseUrl, n.ToString());
                logger.Info($"Get on {url}");
                try
                {
                    ResultWeb = Web.DownloadStringData(url, IgCookies).GetAwaiter().GetResult();
                    if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        // Showcase
                        var ShowcaseElement = htmlDocument.QuerySelector("div.profile-private-page-library-tab-showcase");
                        if (ShowcaseElement != null)
                        {
                            // End list ?
                            var noElement = ShowcaseElement.QuerySelector("div.profile-private-page-library-no-results");
                            if (noElement != null)
                            {
                                logger.Info($"End list");
                                isGood = true;
                                return OwnedGames;
                            }

                            foreach (var SearchElement in ShowcaseElement.QuerySelectorAll("ul.profile-private-page-library-sublist"))
                            {
                                var Element = SearchElement.QuerySelector("div.profile-private-page-library-subitem");
                                string GameId = Element?.GetAttribute("id").Replace("showcase-item-", string.Empty);
                                if (GameId.IsNullOrEmpty())
                                {
                                    logger.Error($"IndiegalaLibrary - No GameId in {Element.InnerHtml}");
                                    continue;
                                }

                                Element = SearchElement.QuerySelector("div.profile-private-showcase-sub-section-row-cont");

                                List<Link> StoreLink = new List<Link>();
                                var tempLink = Element.QuerySelector("a");
                                if (tempLink != null)
                                {
                                    StoreLink.Add(new Link("Store", tempLink.GetAttribute("href")));
                                }

                                string Name = SearchElement.QuerySelector("a.library-showcase-title")?.InnerHtml;
                                if (Name.IsNullOrEmpty())
                                {
                                    logger.Error($"No Name in {Element.InnerHtml}");
                                    continue;
                                }

                                string UrlDownload = string.Empty;
                                var DownloadAction = new GameAction();
                                var GameActions = new List<GameAction>();

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
                                    logger.Warn($"UrlDownload not found for {Name}");
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
                                    gameInfo = CheckIsInstalled(Plugin, PluginSettings, gameInfo);
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
                            logger.Warn($"No showcase data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"Not find showcase");
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


        private GameMetadata CheckIsInstalled(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings, GameMetadata gameMetadata)
        {
            bool IsInstalled = false;

            // Check with defined installation
            Game game = Plugin.PlayniteApi.Database.Games.Where(x => x.GameId == gameMetadata.GameId).FirstOrDefault();
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
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(Plugin.PlayniteApi, gameMetadata.GameId);
                    Common.LogDebug(true, Serialization.ToJson($"clientGameInfo: {clientGameInfo}"));
                    if (clientGameInfo != null && userCollection != null)
                    {
                        string PathDirectory = Path.Combine(InstallPathClient, userCollection.prod_slugged_name);
                        string ExeFile = clientGameInfo.exe_path ?? string.Empty;
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
                                var gameActions = new List<GameAction>();
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
                    Plugin.PlayniteApi.Database.Games.Update(game);
                });
            }


            return gameMetadata;
        }


        public static GameMetadata GetMetadataWithClient(IPlayniteAPI PlayniteApi, string Id)
        {
            if (IndieglaClient.ClientData != null)
            {
                UserCollection userCollection = IndieglaClient.ClientData.data.showcase_content.content.user_collection.Find(x => x.id.ToString() == Id);

                if (userCollection != null)
                {
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(PlayniteApi, Id);

                    if (clientGameInfo != null)
                    {
                        int? CommunityScore = null;
                        if (clientGameInfo.rating.avg_rating != null)
                        {
                            CommunityScore = (int)clientGameInfo.rating.avg_rating * 20;
                        }

                        var gameMetadata = new GameMetadata()
                        {
                            Links = new List<Link>(),
                            Tags = clientGameInfo.tags?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            Genres = clientGameInfo.categories?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            Features = clientGameInfo.specs?.Select(x => new MetadataNameProperty(x)).Cast<MetadataProperty>().ToHashSet(),
                            GameActions = new List<GameAction>(),
                            CommunityScore = CommunityScore,
                            Description = clientGameInfo.description_long
                        };

                        string BackgroundImage = string.Empty;
                        if (!userCollection.prod_dev_cover.IsNullOrEmpty())
                        {
                            var bg = new MetadataFile(string.Format(ProdCoverUrl, userCollection.prod_dev_namespace, userCollection.prod_id_key_name, userCollection.prod_dev_cover));
                            gameMetadata.BackgroundImage = bg;
                        }

                        return gameMetadata;
                    }
                }
            }

            return null;
        }
    }
}
