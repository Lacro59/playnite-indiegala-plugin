using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK;
using Playnite.SDK.Models;
using CommonPluginsShared;
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using IndiegalaLibrary.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace IndiegalaLibrary.Services
{
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

        public bool isConnected = false;
        public bool isLocked = false;


        public IndiegalaAccountClient(IWebView webView)
        {
            _webView = webView;
        }

        public void Login(IWebView view)
        {
            logger.Info("Login()");

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

        public bool GetIsUserLoggedIn()
        {
            _webView.NavigateAndWait(loginUrl);

            isLocked = _webView.GetPageSource().ToLower().IndexOf("profile locked") > -1;

            Common.LogDebug(true, $"{_webView.GetCurrentAddress()} - isLocked: {isLocked}");

            if (_webView.GetCurrentAddress().StartsWith(loginUrl))
            {
                logger.Warn("User is not connected");
                isConnected = false;
                return false;
            }
            logger.Info("User is connected");
            isConnected = true;
            return true;
        }

        public bool GetIsUserLocked()
        {
            return isLocked;
        }


        public static List<ResultResponse> SearchGame(string GameName)
        {
            List<ResultResponse> Result = new List<ResultResponse>();

            List<ResultResponse> ResultStore = SearchGameStore(GameName);
            List<ResultResponse> ResultShowcase = SearchGameShowcase(GameName);

            Result = Result.Concat(ResultStore).Concat(ResultShowcase).ToList();
            Common.LogDebug(true, $"Result: {JsonConvert.SerializeObject(Result)}");

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

                if (searchResponse != null && !searchResponse.html.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument htmlDocument = parser.Parse(searchResponse.html.Replace("\\", string.Empty));

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

                            if (searchResponse != null && !searchResponse.html.IsNullOrEmpty())
                            {
                                HtmlParser parser = new HtmlParser();
                                IHtmlDocument htmlDocument = parser.Parse(searchResponse.html.Replace("\\", string.Empty));

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
                searchResponse = JsonConvert.DeserializeObject<SearchResponse>(ResponseSearch);
            }
            catch
            {

            }

            Common.LogDebug(true, $"searchResponse: {JsonConvert.SerializeObject(searchResponse)}");

            return searchResponse;
        }


        public List<GameInfo> GetOwnedGames()
        {
            List<GameInfo> OwnedGames = new List<GameInfo>();

            List<GameInfo> OwnedGamesShowcase = GetOwnedGamesShowcase();
            List<GameInfo> OwnedGamesBundle = GetOwnedGamesBundle();
            List<GameInfo> OwnedGamesStore = GetOwnedGamesStore();

            OwnedGames = OwnedGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();
            Common.LogDebug(true, $"OwnedGames: {JsonConvert.SerializeObject(OwnedGames)}");

            return OwnedGames;
        }

        private List<GameInfo> GetOwnedGamesBundle()
        {
            var OwnedGames = new List<GameInfo>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(bundleUrl, n.ToString());
                logger.Info($"Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();

                    Common.LogDebug(true, $"webView on {_webView.GetCurrentAddress()}");

                    if (_webView.GetCurrentAddress().IndexOf("https://www.indiegala.com/library/bundle/") == -1)
                    {
                        logger.Warn($"webView on {_webView.GetCurrentAddress()}");
                    }
                    else if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        // Showcase
                        var ShowcaseElement = htmlDocument.QuerySelector("div.profile-private-page-library-tab-bundle");
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
                                foreach (var listItem in SearchElement.QuerySelectorAll("li.profile-private-page-library-subitem"))
                                {
                                    Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                                    string GameId = string.Empty;
                                    string Name = string.Empty;
                                    var GameActions = new List<GameAction>();
                                    List<Link> StoreLink = new List<Link>();
                                    string BackgroundImage = string.Empty;

                                    Name = listItem.QuerySelector("figcaption div.profile-private-page-library-title div").InnerHtml;
                                    GameId = Name.GetSHA256Hash();

                                    BackgroundImage = listItem.QuerySelector("figure img.async-img-load").GetAttribute("src");
                                    if (!BackgroundImage.Contains(".jpg") && !BackgroundImage.Contains(".png"))
                                    {
                                        BackgroundImage = string.Empty;
                                    }

                                    var tempLink = listItem.QuerySelector("figure a");
                                    if (tempLink != null)
                                    {
                                        StoreLink.Add(new Link("Store", tempLink.GetAttribute("href")));
                                    }

                                    try
                                    {
                                        var UrlDownload = listItem.QuerySelector("figcaption a.bg-gradient-light-blue").GetAttribute("href"); 
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
                                    }
                                    catch
                                    {
                                        logger.Error($"UrlDownload not found for {Name}");
                                    }

                                    var tempGameInfo = new GameInfo()
                                    {
                                        Source = "Indiegala",
                                        GameId = GameId,
                                        Name = Name,
                                        Platform = "PC",
                                        GameActions = GameActions,
                                        LastActivity = null,
                                        Playtime = 0,
                                        Links = StoreLink
                                        //CoverImage = BackgroundImage,
                                    };

                                    Common.LogDebug(true, $"Find {JsonConvert.SerializeObject(tempGameInfo)}");

                                    var HaveKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial");
                                    if (HaveKey == null)
                                    {
                                        Common.LogDebug(true, $"Find bundle - {GameId} {Name}");
                                        OwnedGames.Add(tempGameInfo);
                                    }
                                    else
                                    {
                                        logger.Info($"Is not a Indiegala game - {GameId} {Name}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            logger.Warn($"No bundle data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"Not find bundle");
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

        private List<GameInfo> GetOwnedGamesStore()
        {
            var OwnedGames = new List<GameInfo>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(storeUrl, n.ToString());
                logger.Info($"Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();
                    Common.LogDebug(true, $"webView on {_webView.GetCurrentAddress()}");

                    if (_webView.GetCurrentAddress().IndexOf("https://www.indiegala.com/library/store/") == -1)
                    {
                        logger.Warn($"webView on {_webView.GetCurrentAddress()}");
                    }
                    else if (!ResultWeb.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        // Showcase
                        var ShowcaseElement = htmlDocument.QuerySelector("div.profile-private-page-library-tab-store");
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
                                foreach (var listItem in SearchElement.QuerySelectorAll("li.profile-private-page-library-subitem"))
                                {
                                    Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                                    string GameId = string.Empty;
                                    string Name = string.Empty;
                                    var GameActionss = new List<GameAction>();
                                    List<Link> StoreLink = new List<Link>();
                                    string BackgroundImage = string.Empty;

                                    Name = listItem.QuerySelector("figcaption div.profile-private-page-library-title div").InnerHtml;
                                    GameId = Name.GetSHA256Hash();

                                    BackgroundImage = listItem.QuerySelector("figure img.async-img-load").GetAttribute("src");
                                    if (!BackgroundImage.Contains(".jpg") && !BackgroundImage.Contains(".png"))
                                    {
                                        BackgroundImage = string.Empty;
                                    }

                                    var tempLink = listItem.QuerySelector("figure a");
                                    if (tempLink != null)
                                    {
                                        StoreLink.Add(new Link("Store", tempLink.GetAttribute("href")));
                                    }
                                    
                                    try
                                    {
                                        var UrlDownload = listItem.QuerySelector("figcaption a.bg-gradient-light-blue").GetAttribute("href");
                                        if (!UrlDownload.IsNullOrEmpty())
                                        {
                                            GameAction DownloadAction = new GameAction()
                                            {
                                                Name = "Download",
                                                Type = GameActionType.URL,
                                                Path = UrlDownload
                                            };

                                            GameActionss = new List<GameAction> { DownloadAction };
                                        }
                                        else
                                        {
                                            logger.Warn($"UrlDownload not found for {Name}");
                                        }
                                    }
                                    catch
                                    {
                                        logger.Error($"UrlDownload not found for {Name}");
                                    }

                                    var tempGameInfo = new GameInfo()
                                    {
                                        Source = "Indiegala",
                                        GameId = GameId,
                                        Name = Name,
                                        Platform = "PC",
                                        GameActions = GameActionss,
                                        LastActivity = null,
                                        Playtime = 0,
                                        Links = StoreLink
                                        //CoverImage = BackgroundImage,
                                    };

                                    Common.LogDebug(true, $"Find {JsonConvert.SerializeObject(tempGameInfo)}");

                                    var HaveKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial");
                                    if (HaveKey == null)
                                    {
                                        Common.LogDebug(true, $"Find store - {GameId} {Name}");
                                        OwnedGames.Add(tempGameInfo);
                                    }
                                    else
                                    {
                                        logger.Info($"Is not a Indiegala game - {GameId} {Name}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            logger.Warn($"No store data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"Not find store");
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

        private List<GameInfo> GetOwnedGamesShowcase()
        {
            var OwnedGames = new List<GameInfo>();

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
                                string GameId = Element.GetAttribute("id").Replace("showcase-item-", string.Empty);

                                Element = SearchElement.QuerySelector("div.profile-private-showcase-sub-section-row-cont");
                                string StoreLink = Element.QuerySelector("a").GetAttribute("href");
                                string BackgroundImage = Element.QuerySelector("img").GetAttribute("src");

                                string Name = SearchElement.QuerySelector("a.library-showcase-title").InnerHtml;
                                string Author = SearchElement.QuerySelector("span.library-showcase-sub-title a").InnerHtml;

                                string UrlDownload = string.Empty;
                                var DownloadAction = new GameAction();
                                var GameActions = new List<GameAction>();
                                try
                                {
                                    UrlDownload = SearchElement.QuerySelector("a.library-showcase-download-btn").GetAttribute("onclick");
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
                                }
                                catch
                                {
                                    logger.Error($"UrlDownload not found for {Name}");
                                }

                                Common.LogDebug(true, $"Find showcase - {GameId} {Name}");

                                OwnedGames.Add(new GameInfo()
                                {
                                    Source = "Indiegala",
                                    GameId = GameId,
                                    Name = Name,
                                    Platform = "PC",
                                    GameActions = GameActions,
                                    LastActivity = null,
                                    Playtime = 0,
                                    Links = new List<Link>()
                                    {
                                        new Link("Store", StoreLink)
                                    }
                                    //CoverImage = BackgroundImage,
                                });
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
    }
}
