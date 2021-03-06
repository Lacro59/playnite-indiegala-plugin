﻿using AngleSharp.Dom.Html;
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
using Playnite.SDK.Data;

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

        private static string urlGetStore = "https://www.indiegala.com/library/get-store-contents";
        private static string urlGetBundle = "https://www.indiegala.com/library/get-bundle-contents";

        public bool isConnected = false;
        public bool isLocked = false;


        public IndiegalaAccountClient(IWebView webView)
        {
            _webView = webView;
        }

        public void Login(IWebView view)
        {
            logger.Info("IndiegalaLibrary - Login()");

            view.LoadingChanged += (s, e) =>
            {
#if DEBUG
                logger.Debug($"IndiegalaLibrary [Ignored] - NavigationChanged - {view.GetCurrentAddress()}");
#endif

                if (view.GetCurrentAddress().IndexOf("https://www.indiegala.com/") > -1 && view.GetCurrentAddress().IndexOf(loginUrl) == -1 && view.GetCurrentAddress().IndexOf(logoutUrl) == -1)
                {
#if DEBUG
                    logger.Debug($"IndiegalaLibrary [Ignored] - _webView.Close();");
#endif
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

#if DEBUG
            logger.Debug($"IndiegalaLibrary [Ignored] - {_webView.GetCurrentAddress()} - isLocked: {isLocked}");
#endif

            if (_webView.GetCurrentAddress().StartsWith(loginUrl))
            {
                logger.Warn("IndiegalaLibrary - User is not connected");
                isConnected = false;
                return false;
            }
            logger.Info("IndiegalaLibrary - User is connected");
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
#if DEBUG
            logger.Debug($"IndiegalaLibrary [Ignored] - Result: {JsonConvert.SerializeObject(Result)}");
#endif
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
                    logger.Warn($"IndiegalaLibrary - No game store search");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IndiegalaLibrary", "Error on SearchGameStore()");
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
                    logger.Info($"IndiegalaLibrary - Search on {url}");
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
                                logger.Warn($"IndiegalaLibrary - Not more showcase search");
                                isGood = true;
                            }
                        }
                        else
                        {
                            logger.Warn($"IndiegalaLibrary - Not find showcase search");
                            isGood = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, "IndiegalaLibrary", "Error in download search");
                        isGood = true;
                    }

                    n++;
                }
            }         
            catch (Exception ex)
            {
                Common.LogError(ex, "IndiegalaLibrary", "Error on SearchGameShowcase()");
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

#if DEBUG
            logger.Debug($"IndiegalaLibrary [Ignored] - ResponseSearch: {ResponseSearch}");
#endif
            SearchResponse searchResponse = new SearchResponse();
            try
            {
                searchResponse = JsonConvert.DeserializeObject<SearchResponse>(ResponseSearch);
            }
            catch
            {

            }
#if DEBUG
            logger.Debug($"IndiegalaLibrary [Ignored] - searchResponse: {JsonConvert.SerializeObject(searchResponse)}");
#endif

            return searchResponse;
        }


        public List<GameInfo> GetOwnedGames()
        {
            List<GameInfo> OwnedGames = new List<GameInfo>();

            List<GameInfo> OwnedGamesShowcase = GetOwnedGamesShowcase();
            List<GameInfo> OwnedGamesBundle = GetOwnedGamesBundleStore(DataType.bundle);
            List<GameInfo> OwnedGamesStore = GetOwnedGamesBundleStore(DataType.store);

            OwnedGames = OwnedGames.Concat(OwnedGamesShowcase).Concat(OwnedGamesBundle).Concat(OwnedGamesStore).ToList();
#if DEBUG
            logger.Debug($"IndiegalaLibrary [Ignored] - OwnedGames: {JsonConvert.SerializeObject(OwnedGames)}");
#endif
            return OwnedGames;
        }

        private List<GameInfo> GetOwnedGamesBundleStore(DataType dataType)
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
                logger.Info($"IndiegalaLibrary - Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();

#if DEBUG
                    logger.Debug($"IndiegalaLibrary [Ignored] - webView on {_webView.GetCurrentAddress()}");
#endif

                    if (_webView.GetCurrentAddress().IndexOf(originUrl.Replace("{0}", string.Empty)) == -1)
                    {
                        logger.Warn($"IndiegalaLibrary - webView on {_webView.GetCurrentAddress()}");
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
                                logger.Info($"IndiegalaLibrary - End list");
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

                                var Cookies = _webView.GetCookies();
                                Cookies = Cookies.Where(x => (bool)(x != null & x.Domain != null & x.Value != null & x?.Domain?.Contains("indiegala")))?.ToList();

                                string response = Web.PostStringDataPayload(urlData, payload, Cookies).GetAwaiter().GetResult();
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
                                    //Common.LogDebug(true, listItem.InnerHtml.Replace(Environment.NewLine, string.Empty));

                                    if (listItem.QuerySelector("i").ClassList.Where(x => x.Contains("fa-windows"))?.Count() == 0)
                                    {
                                        continue;
                                    }

                                    string GameId = string.Empty;
                                    string Name = string.Empty;
                                    var OtherActions = new List<GameAction>();
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

                                        OtherActions = new List<GameAction> { DownloadAction };
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
                                        OtherActions = OtherActions,
                                        Links = StoreLink
                                    };

                                    //tempGameInfo = CheckIsInstalled(Plugin, PluginSettings, tempGameInfo);

                                    //Common.LogDebug(true, $"Find {Serialization.ToJson(tempGameInfo)}");

                                    var HaveKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial");
                                    if (HaveKey == null)
                                    {
                                        //Common.LogDebug(true, $"Find {originData} - {GameId} {Name}");
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
                            logger.Warn($"IndiegalaLibrary - No {originData} data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"IndiegalaLibrary - Not find {originData}");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IndiegalaLibrary", "Error in download library");
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
#if DEBUG
                if (n == 2)
                {
                    //n = 50;
                }
#endif

                url = string.Format(showcaseUrl, n.ToString());
                logger.Info($"IndiegalaLibrary - Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();

#if DEBUG
                    logger.Debug($"IndiegalaLibrary [Ignored] - webView on {_webView.GetCurrentAddress()}");
#endif

                    if (_webView.GetCurrentAddress().IndexOf("https://www.indiegala.com/library/showcase/") == -1)
                    {
                        logger.Warn($"IndiegalaLibrary - webView on {_webView.GetCurrentAddress()}");
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
                                logger.Info($"IndiegalaLibrary - End list");
                                isGood = true;
                                return OwnedGames;
                            }

                            foreach (var SearchElement in ShowcaseElement.QuerySelectorAll("ul.profile-private-page-library-sublist"))
                            {
                                var Element = SearchElement.QuerySelector("div.profile-private-page-library-subitem");
                                string GameId = Element?.GetAttribute("id")?.Replace("showcase-item-", string.Empty);
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
                                    logger.Error($"IndiegalaLibrary - No Name in {Element.InnerHtml}");
                                    continue;
                                }

                                string UrlDownload = string.Empty;
                                var DownloadAction = new GameAction();
                                var OtherActions = new List<GameAction>();
                                UrlDownload = SearchElement.QuerySelector("a.library-showcase-download-btn")?.GetAttribute("onclick");
                                if (!UrlDownload.IsNullOrEmpty())
                                {
                                    UrlDownload = UrlDownload.Replace("location.href='", string.Empty);
                                    UrlDownload = UrlDownload.Substring(0, UrlDownload.Length - 1);
                                    DownloadAction = new GameAction()
                                    {
                                        Name = "Download",
                                        Type = GameActionType.URL,
                                        Path = UrlDownload,
                                        IsHandledByPlugin = true
                                    };

                                    OtherActions = new List<GameAction> { DownloadAction };
                                }
                                else
                                {
                                    logger.Warn($"IndiegalaLibrary - UrlDownload not found for {Name}");
                                }

#if DEBUG
                                logger.Debug($"IndiegalaLibrary [Ignored] - Find showcase - {GameId} {Name}");
#endif

                                OwnedGames.Add(new GameInfo()
                                {
                                    Source = "Indiegala",
                                    GameId = GameId,
                                    Name = Name,
                                    Platform = "PC",
                                    OtherActions = OtherActions,
                                    Links = StoreLink
                                });
                            }
                        }
                        else
                        {
                            logger.Warn($"IndiegalaLibrary - No showcase data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"IndiegalaLibrary - Not find showcase");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IndiegalaLibrary", "Error in download library");
                    isGood = true;
                    return OwnedGames;
                }

                n++;
            }

            return OwnedGames;
        }
    }


    public class StoreBundleResponse
    {
        public string status { get; set; }
        public string code { get; set; }
        public string html { get; set; }
    }
}
