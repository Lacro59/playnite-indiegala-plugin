using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaAccountClient
    {
        private const string loginUrl = @"https://www.indiegala.com/login";
        private const string logoutUrl = @"https://www.indiegala.com/logout";
        private const string showcaseUrl = @"https://www.indiegala.com/library/showcase/{0}";

        private ILogger logger = LogManager.GetLogger();
        private IWebView webView;

        public bool IsConnected = false;

        public IndiegalaAccountClient(IWebView webView)
        {
            this.webView = webView;
        }

        public void Login()
        {
            webView.NavigationChanged += (s, e) =>
            {
                logger.Debug("IndiegalaLibrary - " + webView.GetCurrentAddress());
                if (webView.GetCurrentAddress().IndexOf(@"https://www.indiegala.com/") > -1 && webView.GetCurrentAddress().IndexOf(loginUrl) == -1)
                {
                    webView.Close();
                }
            };

            webView.Navigate(loginUrl);
            webView.OpenDialog();
        }

        public bool GetIsUserLoggedIn()
        {
            webView.NavigateAndWait(showcaseUrl);
            if (webView.GetCurrentAddress().IndexOf(@"https://www.indiegala.com/") > -1 && webView.GetCurrentAddress().IndexOf(loginUrl) == -1)
            {
                IsConnected = true;
                return true;
            }
            IsConnected = false;
            return false;
        }

        public List<GameInfo> GetOwnedGames()
        {
            var OwnedGames = new List<GameInfo>();

            int n = 1;
            string ResultWeb = "";
            string url = "";
            bool isGood = false;
            while (!isGood)
            {
                url = string.Format(showcaseUrl, n);
                logger.Debug($"IndiegalaLibrary - {url}");
                try
                {
                    webView.NavigateAndWait(url);
                    ResultWeb = webView.GetPageSource();
                    if (!ResultWeb.IsNullOrEmpty() && ResultWeb.IndexOf("Your showcase list is actually empty") == -1)
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                        foreach (var SearchElement in htmlDocument.QuerySelectorAll("ul.profile-private-page-library-sublist"))
                        {
                            var Element = SearchElement.QuerySelector("div.profile-private-page-library-subitem");
                            string GameId = Element.GetAttribute("id").Replace("showcase-item-", "");

                            Element = SearchElement.QuerySelector("div.profile-private-showcase-sub-section-row-cont");
                            string StoreLink = Element.QuerySelector("a").GetAttribute("href");
                            string BackgroundImage = Element.QuerySelector("img").GetAttribute("src");

                            string Name = SearchElement.QuerySelector("a.library-showcase-title").InnerHtml;
                            string Author = SearchElement.QuerySelector("span.library-showcase-sub-title a").InnerHtml;

                            logger.Debug($"IndiegalaLibrary - Find {GameId} {Name}");

                            OwnedGames.Add(new GameInfo()
                            {
                                Source = "Indiegala",
                                GameId = GameId,
                                Name = Name,
                                LastActivity = null,
                                Playtime = 0,
                                Links = new List<Link>()
                                {
                                    new Link("Store", StoreLink)
                                },
                                BackgroundImage = BackgroundImage,
                            });
                        }
                    }
                    else
                    {
                        isGood = true;
                        return OwnedGames;
                    }

                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                    {
                        Common.LogError(ex, "IndiegalaLibrary", "Error in download library");
                        isGood = true;
                        return OwnedGames;
                    }
                }

                n++;
            }

            return OwnedGames;
        }

    }
}
