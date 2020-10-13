using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using PluginCommon.PlayniteResources;
using PluginCommon.PlayniteResources.API;
using PluginCommon.PlayniteResources.Common;
using PluginCommon.PlayniteResources.Converters;
using System;
using System.Collections.Generic;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaAccountClient
    {
        private ILogger logger = LogManager.GetLogger();
        private IWebView _webView;

        private const string loginUrl = "https://www.indiegala.com/login";
        private const string logoutUrl = "https://www.indiegala.com/logout";
        private const string libraryUrl = "https://www.indiegala.com/library";
        private const string showcaseUrl = "https://www.indiegala.com/library/showcase/{0}";

        public bool isConnected = false;


        public IndiegalaAccountClient(IWebView webView)
        {
            _webView = webView;
        }

        public void Login()
        {
            logger.Info("IndiegalaLibrary - Login()");

            _webView.LoadingChanged += (s, e) =>
            {
#if DEBUG
                logger.Debug($"IndiegalaLibrary - NavigationChanged - {_webView.GetCurrentAddress()}");
#endif

                if (_webView.GetCurrentAddress().IndexOf("https://www.indiegala.com/") > -1 && _webView.GetCurrentAddress().IndexOf(loginUrl) == -1 && _webView.GetCurrentAddress().IndexOf(logoutUrl) == -1)
                {
#if DEBUG
                    logger.Debug($"IndiegalaLibrary - _webView.Close();");
#endif
                    isConnected = true;
                    _webView.Close();
                }
            };

            isConnected = false;
            _webView.Navigate(logoutUrl);
            _webView.OpenDialog();
        }

        public bool GetIsUserLoggedIn()
        {
            _webView.NavigateAndWait(loginUrl);

#if DEBUG
            logger.Debug($"IndiegalaLibrary - {_webView.GetCurrentAddress()}");
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

        public List<GameInfo> GetOwnedGames()
        {
            var OwnedGames = new List<GameInfo>();

            int n = 1;
            string ResultWeb = string.Empty;
            string url = string.Empty;
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(showcaseUrl, n.ToString());
                logger.Info($"IndiegalaLibrary - Get on {url}");
                try
                {
                    _webView.NavigateAndWait(url);
                    ResultWeb = _webView.GetPageSource();

#if DEBUG
                    logger.Debug($"IndiegalaLibrary - webView on {_webView.GetCurrentAddress()}");
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
                                string GameId = Element.GetAttribute("id").Replace("showcase-item-", string.Empty);

                                Element = SearchElement.QuerySelector("div.profile-private-showcase-sub-section-row-cont");
                                string StoreLink = Element.QuerySelector("a").GetAttribute("href");
                                string BackgroundImage = Element.QuerySelector("img").GetAttribute("src");

                                string Name = SearchElement.QuerySelector("a.library-showcase-title").InnerHtml;
                                string Author = SearchElement.QuerySelector("span.library-showcase-sub-title a").InnerHtml;

                                string UrlDownload = string.Empty;
                                var DownloadAction = new GameAction();
                                var OtherActions = new List<GameAction>();
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
                                            Path = UrlDownload,
                                            IsHandledByPlugin = true
                                        };

                                        OtherActions = new List<GameAction> { DownloadAction };
                                    }
                                    else
                                    {
                                        logger.Warn($"IndiegalaLibrary - UrlDownload not found for {Name}");
                                    }
                                }
                                catch
                                {
                                    logger.Error($"IndiegalaLibrary - UrlDownload not found for {Name}");
                                }

#if DEBUG
                                logger.Debug($"IndiegalaLibrary - Find {GameId} {Name}");
#endif

                                OwnedGames.Add(new GameInfo()
                                {
                                    Source = "Indiegala",
                                    GameId = GameId,
                                    Name = Name,
                                    OtherActions = OtherActions,
                                    LastActivity = null,
                                    Playtime = 0,
                                    Links = new List<Link>()
                                    {
                                        new Link("Store", StoreLink)
                                    },
                                    CoverImage = BackgroundImage,
                                });
                            }
                        }
                        else
                        {
                            logger.Warn($"IndiegalaLibrary - No data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        logger.Warn($"IndiegalaLibrary - Not find Showcase");
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
}
