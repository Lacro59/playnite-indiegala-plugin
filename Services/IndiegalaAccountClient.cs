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
using CommonPluginsPlaynite.Common;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using Playnite.SDK.Metadata;
using System.Windows;

namespace IndiegalaLibrary.Services
{
    internal enum DataType
    {
        bundle, store
    }


    public class IndiegalaAccountClient
    {
        private static ILogger logger = LogManager.GetLogger();
        private IWebView _webView;

        private static string baseUrl = "https://www.indiegala.com";
        private const string loginUrl = "https://www.indiegala.com/login";
        private const string logoutUrl = "https://www.indiegala.com/logout";
        private const string libraryUrl = "https://www.indiegala.com/library";
        private const string showcaseUrl = "https://www.indiegala.com/library/showcase/{0}";
        private const string bundleUrl = "https://www.indiegala.com/library/bundle/{0}";
        private const string storeUrl = "https://www.indiegala.com/library/store/{0}";
        private static string storeSearch = "https://www.indiegala.com/search/query";
        private static string showcaseSearch = "https://www.indiegala.com/showcase/ajax/{0}";

        private static string apiUrl = "https://www.indiegala.com/login_new/user_info";

        private const string ProdCoverUrl = "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodcover/{2}";


        public bool isConnected = false;
        public bool isLocked = false;


        private List<HttpCookie> ClientCookies = new List<HttpCookie>();


        private static List<UserCollection> userCollections = new List<UserCollection>();


        public IndiegalaAccountClient(IWebView webView)
        {
            _webView = webView;
        }


        public void LoginWithClient()
        {
            logger.Info("LoginWithClient()");

            isConnected = false;
            ResetClientCookies();

            IndieglaClient indieglaClient = new IndieglaClient();
            indieglaClient.Open();
        }

        public void LoginWithoutClient(IWebView view)
        {
            logger.Info("LoginWithoutClient()");

            isConnected = false;
            ResetClientCookies();

            view.LoadingChanged += (s, e) =>
            {
                Common.LogDebug(true, $"NavigationChanged - {view.GetCurrentAddress()}");

                if (view.GetCurrentAddress().IndexOf("https://www.indiegala.com/") > -1 && view.GetCurrentAddress().IndexOf(loginUrl) == -1 && view.GetCurrentAddress().IndexOf(logoutUrl) == -1)
                {
                    Common.LogDebug(true, $"_webView.Close();");
                    isConnected = true;
                    view.Close();
                }
            };

            isConnected = false;
            view.Navigate(logoutUrl);
            view.OpenDialog();
        }

        // TODO Used Cookies files
        private void GetClientCookies()
        {
            if (ClientCookies.Count == 3)
            {
                return;
            }

            ClientCookies = new List<HttpCookie>();

            try
            {
                if (File.Exists(IndieglaClient.ConfigFile))
                {
                    foreach (var CookieString in IndieglaClient.ClientData.data.cookies)
                    {
                        HttpCookie httpCookie = new HttpCookie
                        {
                            Creation = DateTime.Now
                        };
                        foreach (var ElementString in CookieString.Split(';').ToList())
                        {
                            var Elements = ElementString.Split('=').ToList();

                            if (Elements[0].ToLower().Trim() == "indiecap")
                            {
                                httpCookie.Name = Elements[0].Trim();
                                httpCookie.Value = string.Empty;
                            }
                            if (Elements[0].ToLower().Trim() == "session")
                            {
                                httpCookie.Name = Elements[0].Trim();
                                httpCookie.Value = Elements[1].Trim();
                            }
                            if (Elements[0].ToLower().Trim() == "auth")
                            {
                                httpCookie.Name = Elements[0].Trim();
                                httpCookie.Value = Elements[1].Trim();
                            }
                            if (Elements[0].ToLower().Trim() == "domain")
                            {
                                httpCookie.Domain = Elements[1].Trim();
                            }
                            if (Elements[0].ToLower().Trim() == "path")
                            {
                                httpCookie.Path = Elements[1].Trim();
                            }
                            if (Elements[0].ToLower().Trim() == "expires")
                            {
                                DateTime.TryParse(Elements[1], out DateTime result);
                                httpCookie.Expires = result;
                            }
                            if (Elements[0].ToLower().Trim() == "max-age")
                            {

                            }
                            if (Elements[0].ToLower().Trim() == "secure")
                            {
                                httpCookie.Secure = true;
                            }
                            if (Elements[0].ToLower().Trim() == "httponly")
                            {
                                httpCookie.HttpOnly = true;
                            }
                        }

                        ClientCookies.Add(httpCookie);
                    }
                }
                else
                {
                    logger.Warn("No config file find");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }

        public void ResetClientCookies()
        {
            ClientCookies = new List<HttpCookie>();
        }


        public bool GetIsUserLoggedInWithClient()
        {
            GetClientCookies();
            string WebData = Web.DownloadStringData(libraryUrl, ClientCookies).GetAwaiter().GetResult();

            isLocked = WebData.Contains("profile locked");
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

        public bool GetIsUserLoggedInWithoutClient()
        {
            _webView.NavigateAndWait(loginUrl);

            isLocked = _webView.GetPageSource().ToLower().Contains("profile locked");

            if (_webView.GetCurrentAddress().StartsWith(loginUrl))
            {
                logger.Warn("User is not connected without client");
                isConnected = false;
                return false;
            }

            logger.Info("User is connected without client");
            ClientCookies = _webView.GetCookies().Where(x => x.Domain.ToLower().Contains("indiegala")).ToList();
            isConnected = true;

            return true;
        }

        public bool GetIsUserLocked()
        {
            return isLocked;
        }


        #region SearchData
        public static List<ResultResponse> SearchGame(string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            List<ResultResponse> ResultStore = SearchGameStore(GameName);
            List<ResultResponse> ResultShowcase = SearchGameShowcase(GameName);

            Result = Result.Concat(ResultStore).Concat(ResultShowcase).ToList();
            Common.LogDebug(true, $"Result: {Serialization.ToJson(Result)}");

            return Result;
        }

        public static List<ResultResponse> SearchGameStore(string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            string payload = "{\"input_string\": \"" + GameName + "\"}";
            try
            {
                string WebResult = Web.PostStringDataPayload(storeSearch, payload).GetAwaiter().GetResult().Replace(Environment.NewLine, string.Empty);
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

        public static List<ResultResponse> SearchGameShowcase(string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            try
            {
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
                        WebResult = Web.DownloadStringData(url).GetAwaiter().GetResult();

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


        public static List<UserCollection> GetUserCollections(IPlayniteAPI PlayniteApi)
        {
            if (IndiegalaAccountClient.userCollections != null && IndiegalaAccountClient.userCollections.Count > 0)
            {
                return IndiegalaAccountClient.userCollections;
            }

            try
            {
                using (var WebViews = PlayniteApi.WebViews.CreateOffscreenView())
                {
                    List<HttpCookie> Cookies = WebViews.GetCookies();
                    Cookies = Cookies.Where(x => (bool)(x?.Domain?.Contains("indiegala"))).ToList();

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
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    PlayniteApi.Resources.GetString("LOCLoginRequired") +
                    System.Environment.NewLine + ex.Message,
                    NotificationType.Error));
            }

            return new List<UserCollection>();
        }



        public List<GameInfo> GetOwnedGames(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings)
        {
            List<GameInfo> OwnedGames = new List<GameInfo>();

            List<GameInfo> OwnedGamesShowcase = new List<GameInfo>();
            OwnedGamesShowcase = GetOwnedGamesShowcase(Plugin, PluginSettings);

            List<GameInfo> OwnedGamesBundle = new List<GameInfo>();
            OwnedGamesBundle = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.bundle);

            List<GameInfo> OwnedGamesStore = new List<GameInfo>();
            OwnedGamesStore = GetOwnedGamesBundleStore(Plugin, PluginSettings, DataType.store);


            OwnedGames = OwnedGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();
            Common.LogDebug(true, $"OwnedGames: {Serialization.ToJson(OwnedGames)}");

            return OwnedGames;
        }


        #region Client
        public static string GetProdSluggedName(IPlayniteAPI PlayniteApi, string GameId)
        {
            List<UserCollection> userCollections = IndiegalaAccountClient.GetUserCollections(PlayniteApi);
            return userCollections?.Find(x => x.id.ToString() == GameId)?.prod_slugged_name;
        } 

        private List<GameInfo> GetOwnedClient(IPlayniteAPI PlayniteApi)
        {
            List<GameInfo> GamesOwnedClient = new List<GameInfo>();


            // TODO Only get basic info
            List<HttpCookie> Cookies = _webView.GetCookies();
            Cookies = Cookies.Where(x => (bool)(x?.Domain?.Contains("indiegala"))).ToList();

            string response = Web.DownloadStringData(apiUrl, Cookies, "galaClient").GetAwaiter().GetResult();

            if (!response.IsNullOrEmpty())
            {
                dynamic data = Serialization.FromJson<dynamic>(response);
                string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                List<UserCollection> userCollections = Serialization.FromJson<List<UserCollection>>(userCollectionString);

                foreach(UserCollection userCollection in userCollections)
                {
                    List<string> Tags = new List<string>();
                    if (userCollection.tags != null && userCollection.tags.Count > 0)
                    {
                        Tags = userCollection.tags.Select(x => x.name).ToList();
                    }

                    GamesOwnedClient.Add(new GameInfo()
                    {
                        Source = "Indiegala",
                        GameId = userCollection.id.ToString(),
                        Name = userCollection.prod_name,
                        Platform = "PC",
                        //GameActions = GameActions,
                        LastActivity = null,
                        Playtime = 0,
                        Tags = Tags
                        //Links = new List<Link>()
                        //{
                        //    new Link("Store", StoreLink)
                        //}
                    });
                }
            }

            /*
            try
            {
                foreach(UserCollection userCollection in IndieglaClient.ClientData.data.showcase_content.content.user_collection)
                {
                    List<string> Developers = null;
                    if (!userCollection.prod_dev_username.IsNullOrEmpty())
                    {
                        Developers = new List<string>();
                        Developers.Add(userCollection.prod_dev_username);
                    }
                    
                    string GameId = userCollection.id.ToString();
                    string Name = userCollection.prod_name;

                    string BackgroundImage = string.Empty;
                    if (!userCollection.prod_dev_cover.IsNullOrEmpty())
                    {
                        BackgroundImage = string.Format(ProdCoverUrl, userCollection.prod_dev_namespace, userCollection.prod_id_key_name, userCollection.prod_dev_cover);
                    }



                    // Game info if exists
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(PlayniteApi, GameId);

                    List<string> Genres = null;
                    List<string> Features = null;
                    List<string> Tags = null;
                    int? CommunityScore = null;

                    if (clientGameInfo != null)
                    {
                        Genres = clientGameInfo.categories;
                        CommunityScore = (int)(clientGameInfo.rating.avg_rating * 20);
                        Features = clientGameInfo.specs;
                        Tags = clientGameInfo.tags;
                    }
                    

                    GameInfo gameInfo = new GameInfo()
                    {
                        Source = "Indiegala",
                        GameId = GameId,
                        Name = Name,
                        Platform = "PC",
                        Developers = Developers,
                        BackgroundImage = BackgroundImage,
                        Genres = Genres,
                        CommunityScore = CommunityScore,
                        Features = Features,
                        Tags = Tags
                    };

                    GamesOwnedClient.Add(gameInfo);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
            */


            return GamesOwnedClient;
        }

        private List<GameInfo> GetInstalledClient(List<GameInfo> OwnedClient)
        {
            try
            {
                List<ClientInstalled> GamesInstalledInfo = IndieglaClient.GetClientGameInstalled();

                foreach (GameInfo gameInfo in OwnedClient)
                {
                    UserCollection userCollection = IndieglaClient.ClientData.data.showcase_content.content.user_collection.Where(x => x.id.ToString() == gameInfo.GameId).FirstOrDefault();

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

                            long Playtime = (long)clientInstalled.playtime;


                            gameInfo.InstallDirectory = GamePath;
                            gameInfo.IsInstalled = true;
                            gameInfo.Playtime = Playtime;
                            gameInfo.GameActions = GameActions;
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


        private List<GameInfo> GetOwnedGamesBundleStore(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings, DataType dataType)
        {
            var OwnedGames = new List<GameInfo>();


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
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();

                    Common.LogDebug(true, $"webView on {_webView.GetCurrentAddress()}");

                    if (_webView.GetCurrentAddress().IndexOf(originUrl.Replace("{0}", string.Empty)) == -1)
                    {
                        logger.Warn($"webView on {_webView.GetCurrentAddress()}");
                    }
                    else if (!ResultWeb.IsNullOrEmpty())
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

                            foreach (var SearchElement in DataElement.QuerySelectorAll("ul.profile-private-page-library-sublist"))
                            {
                                foreach (var listItem in SearchElement.QuerySelectorAll("li.profile-private-page-library-subitem"))
                                {
                                    Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                                    string GameId = string.Empty;
                                    string Name = string.Empty;
                                    var GameActions = new List<GameAction>();
                                    List<Link> StoreLink = new List<Link>();
                                    string BackgroundImage = string.Empty;

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

                                    var tempGameInfo = new GameInfo()
                                    {
                                        Source = "Indiegala",
                                        GameId = GameId,
                                        Name = Name,
                                        Platform = "PC",
                                        GameActions = GameActions,
                                        Links = StoreLink
                                    };

                                    tempGameInfo = CheckIsInstalled(Plugin, PluginSettings, tempGameInfo);

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

        private List<GameInfo> GetOwnedGamesShowcase(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings)
        {
            var OwnedGames = new List<GameInfo>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
#if DEBUG
                if (n > 1)
                {
                    n = 100;
                }
#endif

                url = string.Format(showcaseUrl, n.ToString());
                logger.Info($"Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();
                    Common.LogDebug(true, $"webView on {_webView.GetCurrentAddress()}");

                    if (_webView.GetCurrentAddress().IndexOf("https://www.indiegala.com/library/showcase/") == -1)
                    {
                        logger.Warn($"webView on {_webView.GetCurrentAddress()}");
                    }
                    else if (!ResultWeb.IsNullOrEmpty())
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

                                GameInfo gameInfo = new GameInfo()
                                {
                                    Source = "Indiegala",
                                    GameId = GameId,
                                    Name = Name,
                                    Platform = "PC",
                                    GameActions = GameActions,
                                    Links = StoreLink
                                };

                                gameInfo = CheckIsInstalled(Plugin, PluginSettings, gameInfo);

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


        private GameInfo CheckIsInstalled(IndiegalaLibrary Plugin, IndiegalaLibrarySettingsViewModel PluginSettings, GameInfo gameInfo)
        {
            bool IsInstalled = false;

            // Check with defined installation
            Game game = Plugin.PlayniteApi.Database.Games.Where(x => x.GameId == gameInfo.GameId).FirstOrDefault();
            if (game != null)
            {
                gameInfo.IsInstalled = false;
                game.IsInstalled = false;

                List<GameAction> gameActions = game.GameActions.Where(x => x.IsPlayAction).ToList();
                foreach(GameAction gameAction in gameActions)
                {
                    string PathPlayAction = Path.Combine
                    (
                        PlayniteTools.StringExpandWithoutStore(game, gameAction.WorkingDir),
                        PlayniteTools.StringExpandWithoutStore(game, gameAction.Path)
                    );

                    if (File.Exists(PathPlayAction))
                    {
                        gameInfo.IsInstalled = true;
                        game.IsInstalled = true;
                        IsInstalled = true;
                        break;
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

                    UserCollection userCollection = IndieglaClient.ClientData.data.showcase_content.content.user_collection.Find(x => x.id.ToString() == gameInfo.GameId);
                    ClientGameInfo clientGameInfo = IndieglaClient.GetClientGameInfo(Plugin.PlayniteApi, gameInfo.GameId);
                    if (clientGameInfo != null)
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
                            gameInfo.InstallDirectory = PathDirectory;
                            gameInfo.IsInstalled = true;

                            if (gameInfo.GameActions != null)
                            {
                                gameInfo.GameActions.Add(new GameAction
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

                                gameInfo.GameActions = gameActions;
                            }
                        }


                        if (game != null)
                        {
                            game.IsInstalled = gameInfo.IsInstalled;
                            game.InstallDirectory = gameInfo.InstallDirectory;
                            game.GameActions = gameInfo.GameActions.ToObservable();
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


            return gameInfo;
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
                        var gameInfo = new GameInfo()
                        {
                            Links = new List<Link>(),
                            Tags = clientGameInfo.tags,
                            Genres = clientGameInfo.categories,
                            Features = clientGameInfo.specs,
                            GameActions = new List<GameAction>(),
                            CommunityScore = (int)(clientGameInfo.rating.avg_rating * 20),
                            Description = clientGameInfo.description_long
                        };

                        var metadata = new GameMetadata()
                        {
                            GameInfo = gameInfo
                        };


                        string BackgroundImage = string.Empty;
                        if (!userCollection.prod_dev_cover.IsNullOrEmpty())
                        {
                            var bg = new MetadataFile(string.Format(ProdCoverUrl, userCollection.prod_dev_namespace, userCollection.prod_id_key_name, userCollection.prod_dev_cover));
                            metadata.BackgroundImage = bg;
                        }

                        return metadata;
                    }
                }
            }

            return null;
        }
    }
}
