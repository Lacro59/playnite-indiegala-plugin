using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaAccountClient: INotifyPropertyChanged
    {
        private const string loginUrl = @"https://www.indiegala.com/login";
        private const string logoutUrl = @"https://www.indiegala.com/logout";
        private const string showcaseUrl = @"https://www.indiegala.com/library/showcase/{0}";

        private ILogger logger = LogManager.GetLogger();
        private IWebView webView;

        private bool isConnected = false;
        public bool IsConnected
        {
            get => isConnected;
            set
            {
                if (value != isConnected)
                {
                    isConnected = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public IndiegalaAccountClient(IWebView webView)
        {
            this.webView = webView;
        }

        public void Login()
        {
            logger.Info("IndiegalaLibrary - Login()");

            webView.NavigationChanged += (s, e) =>
            {
                if (webView.GetCurrentAddress().IndexOf(@"https://www.indiegala.com/") > -1 && webView.GetCurrentAddress().IndexOf(loginUrl) == -1 && webView.GetCurrentAddress().IndexOf(logoutUrl) == -1)
                {
                    webView.Close();
                }
            };

            webView.Navigate(logoutUrl);
            webView.OpenDialog();
        }

        public bool GetIsUserLoggedIn()
        {
            webView.NavigateAndWait(loginUrl);
            if (webView.GetCurrentAddress().StartsWith(loginUrl))
            {
                logger.Warn("IndiegalaLibrary - GetIsUserLoggedIn() - User is not connected");
                isConnected = false;
                return false;
            }
            logger.Info("IndiegalaLibrary - GetIsUserLoggedIn() - User is connected");
            isConnected = true;
            return true;
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
                logger.Info($"IndiegalaLibrary - Get on {showcaseUrl}");
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

                            logger.Info($"IndiegalaLibrary - Find {GameId} {Name}");

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
                                CoverImage = BackgroundImage,
                            });
                        }
                    }
                    else
                    {
                        logger.Info($"IndiegalaLibrary - End list");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (WebException ex)
                {
                    Common.LogError(ex, "IndiegalaLibrary", "Error in download library");
                    logger.Debug("ResultWeb");
                    isGood = true;
                    return OwnedGames;
                }

                n++;
            }

            return OwnedGames;
        }
    }
}
