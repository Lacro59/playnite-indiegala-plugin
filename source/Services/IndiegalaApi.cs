using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using IndiegalaLibrary.Models;
using IndiegalaLibrary.Models.Api;
using IndiegalaLibrary.Models.GalaClient;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace IndiegalaLibrary.Services
{
    public enum DataType { bundle, store }

    public enum ConnectionState { Logged, Locked, Unlogged }


    public class IndiegalaApi : ObservableObject
    {
        private static ILogger Logger => LogManager.GetLogger();

        #region Urls
        private static string UrlFreebies => "https://freebies.indiegala.com";
        private static string UrlBase => "https://www.indiegala.com";
        private static string UrlUserInfo => UrlBase + "/login_new/user_info";

        private static string LoginUrl => UrlBase + "/login";
        private static string LogoutUrl => UrlBase + "/logout";
        private static string LibraryUrl => UrlBase + "/library";
        private static string ShowcaseUrl => LibraryUrl + "/showcase/{0}";
        private static string UrlBundle => LibraryUrl + "/bundle/{0}";
        private static string UrlStore => LibraryUrl + "/store/{0}";
        private static string UrlGetStore => LibraryUrl + "/get-store-contents";
        private static string UrlGetBundle => LibraryUrl + "/get-bundle-contents";

        private static string UrlSearch => UrlBase + "/search/query";
        private static string ShowcaseSearch => UrlBase + "/showcase/ajax/{0}";

        private static string UrlProdCover => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodcover/{2}";
        private static string UrlProdMain => "https://www.indiegalacdn.com/imgs/devs/{0}/products/{1}/prodmain/{2}";

        private static string UrlGameDetails => @"https://developers.indiegala.com/get_product_info?dev_id={0}&prod_name={1}";
        #endregion

        protected bool? _isUserLoggedIn;
        public bool IsUserLoggedIn
        {
            get
            {
                if (_isUserLoggedIn == null)
                {
                    _isUserLoggedIn = GetIsUserLoggedIn();
                }
                return (bool)_isUserLoggedIn;
            }

            set => SetValue(ref _isUserLoggedIn, value);
        }

        private string FileCookies { get; }
        private bool UseClient { get; }

        private static Regex BundleResponseRegex => new Regex(@"^\{\s*""status"":\s*""(?<status>\w+)"",\s*""code"":\s*""(?<code>\w*)"",\s*""html"":\s*""(?<html>.*)""}$", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);


        public IndiegalaApi(string pluginUserDataPath, bool useClient)
        {
            UseClient = useClient;
            FileCookies = Path.Combine(pluginUserDataPath, CommonPlayniteShared.Common.Paths.GetSafePathName($"Indiegala_Cookies.dat"));
        }

        #region Cookies
        /// <summary>
        /// Read the last identified cookies stored.
        /// </summary>
        /// <returns></returns>
        internal List<HttpCookie> GetStoredCookies()
        {
            if (File.Exists(FileCookies))
            {
                try
                {
                    List<HttpCookie> StoredCookies = Serialization.FromJson<List<HttpCookie>>(
                        Encryption.DecryptFromFile(
                            FileCookies,
                            Encoding.UTF8,
                            WindowsIdentity.GetCurrent().User.Value));

                    List<HttpCookie> findExpired = StoredCookies.FindAll(x => x.Expires != null && (DateTime)x.Expires <= DateTime.Now);
                    if (findExpired?.Count > 0)
                    {
                        Logger.Info("Expired cookies");
                    }
                    return StoredCookies;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Failed to load saved cookies");
                }
            }

            return null;
        }

        /// <summary>
        /// Save the last identified cookies stored.
        /// </summary>
        /// <param name="httpCookies"></param>
        internal bool SetStoredCookies(List<HttpCookie> httpCookies)
        {
            try
            {
                FileSystem.CreateDirectory(Path.GetDirectoryName(FileCookies));
                Encryption.EncryptToFile(
                    FileCookies,
                    Serialization.ToJson(httpCookies),
                    Encoding.UTF8,
                    WindowsIdentity.GetCurrent().User.Value);
                return true;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Failed to save cookies");
            }

            return false;
        }

        /// <summary>
        /// Get cookies in WebView or another method.
        /// </summary>
        /// <returns></returns>
        internal static List<HttpCookie> GetWebCookies()
        {
            List<HttpCookie> httpCookies = new List<HttpCookie>();
            using (IWebView webViewOffscreen = API.Instance.WebViews.CreateOffscreenView())
            {
                httpCookies = webViewOffscreen.GetCookies();
                httpCookies = httpCookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                webViewOffscreen.DeleteDomainCookies("www.indiegala.com");
                webViewOffscreen.DeleteDomainCookies(".indiegala.com");
            }
            return httpCookies;
        }
        #endregion

        #region Configuration
        public void ResetIsUserLoggedIn()
        {
            _isUserLoggedIn = null;
        }

        protected bool GetIsUserLoggedIn()
        {
            try
            {
                string response = Web.DownloadStringData(UrlUserInfo, GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                return !response.IsNullOrEmpty() && response.Contains("\"user_found\": \"true\"");
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Login()
        {
            LoginWithoutClient();
        }
        #endregion

        #region Games Showcase
        private List<UserCollection> GetUserShowcase()
        {
            if (!IsUserLoggedIn)
            {
                NotAuthenticated();
                return new List<UserCollection>();
            }

            try
            {
                string response = Web.DownloadStringData(UrlUserInfo, GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                if (!response.IsNullOrEmpty() && response.Contains("\"user_found\": \"true\""))
                {
                    dynamic data = Serialization.FromJson<dynamic>(response);
                    string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                    _ = Serialization.TryFromJson(userCollectionString, out List<UserCollection> userCollections);
                    return userCollections;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            NotAuthenticated();
            return new List<UserCollection>();
        }

        public List<GameMetadata> GetOwnedShowcase(bool withDetails)
        {
            List<GameMetadata> ownedGamesShowcase = new List<GameMetadata>();
            List<UserCollection> userShowcase = GetUserShowcase();

            userShowcase.ForEach(x =>
            {
                GameMetadata gameMetadata = GetGameMetadata(x, withDetails);
                if (gameMetadata != null)
                {
                    string gameId = gameMetadata.GameId;
                    string[] gameIdSplited = gameId.Split('|');
                    if (gameIdSplited.Count() > 1)
                    {
                        gameId = gameIdSplited[1];
                    }
                    gameMetadata.IsInstalled = IndiegalaClient.GameIsInstalled(gameId) != null;
                    ownedGamesShowcase.Add(gameMetadata);
                }
            });

            return ownedGamesShowcase;
        }

        public GameMetadata GetShowCaseDetails(GameMetadata gameMetadata, string prod_dev_namespace, string prod_slugged_name)
        {
            try
            {
                ApiGameDetails data = GetGameDetails(prod_dev_namespace, prod_slugged_name);

                List<GameAction> gameActions = new List<GameAction>();
                if (!(data?.ProductData.DownloadableVersions?.Win?.IsNullOrEmpty() ?? false))
                {
                    GameAction downloadAction = new GameAction()
                    {
                        Name = ResourceProvider.GetString("LOCDownloadLabel") ?? "Download",
                        Type = GameActionType.URL,
                        Path = data?.ProductData.DownloadableVersions?.Win
                    };
                    gameActions.Add(downloadAction);
                };

                int? communityScore = null;
                if (data.ProductData.Rating.AvgRating != null)
                {
                    communityScore = (int)data.ProductData.Rating.AvgRating * 20;
                }

                gameMetadata.GameActions = gameActions;
                gameMetadata.Genres = data?.ProductData?.Categories?.Where(y => !y.Name.IsNullOrEmpty()).Select(y => new MetadataNameProperty(y.Name)).Cast<MetadataProperty>().ToHashSet() ?? null;
                gameMetadata.Features = data?.ProductData?.Specs?.Where(y => !y.Name.IsNullOrEmpty()).Select(y => new MetadataNameProperty(y.Name)).Cast<MetadataProperty>().ToHashSet() ?? null;
                gameMetadata.CommunityScore = communityScore;
                gameMetadata.Description = data?.ProductData?.OtherText?.IsNullOrEmpty() ?? false ? data?.ProductData?.Description ?? string.Empty : data?.ProductData?.OtherText;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
            }

            return gameMetadata;
        }

        public GameMetadata GetGameMetadata(UserCollection userCollection, bool withDetails)
        {
            try
            {
                MetadataFile backgroundImage = null;
                if (!userCollection.ProdDevCover.IsNullOrEmpty())
                {
                    backgroundImage = new MetadataFile(string.Format(UrlProdCover, userCollection.ProdDevNamespace, userCollection.ProdIdKeyName, userCollection.ProdDevCover));
                }

                MetadataFile coverImage = null;
                if (!userCollection.ProdDevImage.IsNullOrEmpty())
                {
                    coverImage = new MetadataFile(string.Format(UrlProdMain, userCollection.ProdDevNamespace, userCollection.ProdIdKeyName, userCollection.ProdDevImage));
                }

                GameMetadata gameMetadata = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Indiegala"),
                    GameId = userCollection.Id + "|" + userCollection.ProdIdKeyName.ToString(),
                    Links = new List<Link> { new Link { Name = ResourceProvider.GetString("LOCMetaSourceStore") ?? "Store", Url = "https://" + userCollection.ProdDevNamespace + ".indiegala.com/" + userCollection.ProdSluggedName } },
                    Name = userCollection.ProdName,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                    Tags = userCollection.Tags?.Where(y => !y.Name.IsNullOrEmpty()).Select(y => new MetadataNameProperty(y.Name)).Cast<MetadataProperty>().ToHashSet() ?? null,
                    ReleaseDate = new ReleaseDate(userCollection.Date),
                    Developers = userCollection.ProdDevUsername.IsEqual("galaFreebies") ? null : new HashSet<MetadataProperty> { new MetadataNameProperty(userCollection.ProdDevUsername) },
                    BackgroundImage = backgroundImage,
                    CoverImage = coverImage
                };

                if (withDetails)
                {
                    gameMetadata = GetShowCaseDetails(gameMetadata, userCollection.ProdDevNamespace, userCollection.ProdSluggedName);
                }

                return gameMetadata;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
                return null;
            }
        }

        public UserCollection GetShowcaseData(string gameId)
        {
            return GetUserShowcase()?.Where(x => x.ProdIdKeyName.ToString().IsEqual(gameId))?.FirstOrDefault() ?? null;
        }
        #endregion

        #region Games Bundle or Store
        public List<GameMetadata> GetOwnedGamesBundleStore(DataType dataType)
        {
            List<GameMetadata> OwnedGames = new List<GameMetadata>();

            if (!IsUserLoggedIn)
            {
                NotAuthenticated();
                return OwnedGames;
            }

            string dataOrigin = string.Empty;
            string urlOrigin = string.Empty;

            switch (dataType)
            {
                case DataType.bundle:
                    dataOrigin = "bundle";
                    urlOrigin = UrlBundle;
                    break;

                case DataType.store:
                    dataOrigin = "store";
                    urlOrigin = UrlStore;
                    break;

                default:
                    break;
            }

            int n = 1;
            bool isGood = false;

            while (!isGood)
            {
                string url = string.Format(urlOrigin, n.ToString());
                Common.LogDebug(true, $"Get on {url}");

                try
                {
                    string webData = Web.DownloadStringData(url, GetStoredCookies()).GetAwaiter().GetResult();
                    if (!webData.IsNullOrEmpty())
                    {
                        HtmlParser parser = new HtmlParser();
                        IHtmlDocument htmlDocument = parser.Parse(webData);

                        IElement DataElement = htmlDocument.QuerySelector($"div.profile-private-page-library-tab-{dataOrigin}");
                        if (DataElement != null)
                        {
                            // End list ?
                            IElement noElement = DataElement.QuerySelector("div.profile-private-page-library-no-results");
                            if (noElement != null)
                            {
                                isGood = true;
                                return OwnedGames;
                            }

                            string csrf = GetCsrf(webData);
                            List<KeyValuePair<string, string>> moreHeader = new List<KeyValuePair<string, string>>
                            {
                                new KeyValuePair<string, string> ("x-csrf-token", csrf),
                                new KeyValuePair<string, string> ("x-csrftoken", csrf)
                            };

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
                                        id = Matches[1].Value.Replace("'", string.Empty);
                                        payload = "{\"version\":\"" + id + "\"}";
                                        urlData = UrlGetBundle;
                                        break;

                                    case DataType.store:
                                        id = Matches[0].Value.Replace("'", string.Empty);
                                        payload = "{\"cart_id\":\"" + id + "\"}";
                                        urlData = UrlGetStore;
                                        break;

                                    default:
                                        break;
                                }

                                string response = Web.PostStringDataPayload(urlData, payload, GetStoredCookies(), moreHeader).GetAwaiter().GetResult();
                                StoreBundleResponse storeBundleResponse = ParseBundleResponse(response);
                                if (!storeBundleResponse?.status?.IsEqual("ok") ?? true)
                                {
                                    Logger.Warn($"No data for {dataOrigin} - {id}");
                                    continue;
                                }

                                parser = new HtmlParser();
                                htmlDocument = parser.Parse(storeBundleResponse.html);

                                List<BundleGameData> gameBundleOrOrderData = GetStoreGameData(storeBundleResponse.html).ToList();
                                foreach (BundleGameData game in gameBundleOrOrderData)
                                {
                                    if (game.IsKey)
                                    {
                                        Logger.Info($"{game.Name} is not a Indiegala game in {dataOrigin}");
                                        continue;
                                    }

                                    GameMetadata gameMetadata = new GameMetadata()
                                    {
                                        Source = new MetadataNameProperty("Indiegala"),
                                        GameId = game.Name.GetSHA256Hash(),
                                        Name = game.Name,
                                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                                        GameActions = new List<GameAction>(),
                                        Links = new List<Link>(),
                                    };

                                    if (!game.StoreUrl.IsNullOrEmpty())
                                    {
                                        gameMetadata.Links.Add(new Link(ResourceProvider.GetString("LOCMetaSourceStore") ?? "Store", game.StoreUrl));
                                    }

                                    if (!game.DownloadUrl.IsNullOrEmpty())
                                    {
                                        gameMetadata.GameActions.Add(new GameAction
                                        {
                                            Name = ResourceProvider.GetString("LOCDownloadLabel") ?? "Download",
                                            Type = GameActionType.URL,
                                            Path = game.DownloadUrl,
                                        });
                                    }

                                    // For Store
                                    if (OwnedGames?.Where(x => x.GameId.IsEqual(gameMetadata.GameId))?.Count() > 0)
                                    {
                                        isGood = true;
                                        return OwnedGames;
                                    }
                                    OwnedGames.Add(gameMetadata);
                                }
                            }
                        }
                        else
                        {
                            Logger.Warn($"No {dataOrigin} data");
                            isGood = true;
                            return OwnedGames;
                        }
                    }
                    else
                    {
                        Logger.Warn($"Not find {dataOrigin}");
                        isGood = true;
                        return OwnedGames;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Error in download library");
                    return OwnedGames;
                }

                n++;
            }

            return OwnedGames;
        }

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
            {
                return new StoreBundleResponse
                {
                    code = match.Groups["code"].Value,
                    status = match.Groups["status"].Value,
                    html = match.Groups["html"].Value.Replace("\\\"", "\"")
                };
            }
            return Serialization.FromJson<StoreBundleResponse>(content);
        }

        private static IEnumerable<BundleGameData> GetStoreGameData(string html)
        {
            HtmlParser parser = new HtmlParser();
            IHtmlDocument htmlDocument = parser.Parse(html);

            foreach (IElement listItem in htmlDocument.QuerySelectorAll("li.profile-private-page-library-subitem"))
            {
                BundleGameData data = ParseGameDataListItemNew(listItem) ?? ParseGameDataListItemOld(listItem);
                if (data != null)
                {
                    data.StoreUrl = listItem.QuerySelector("figure a")?.GetAttribute("href");
                    data.IsKey = listItem.QuerySelector("figcaption input.profile-private-page-library-key-serial") != null;
                    yield return data;
                }
            }
        }

        private static BundleGameData ParseGameDataListItemOld(IElement listItem)
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

        private static BundleGameData ParseGameDataListItemNew(IElement listItem)
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
        #endregion

        private ApiGameDetails GetGameDetails(string prod_dev_namespace, string prod_slugged_name)
        {
            if (!IsUserLoggedIn)
            {
                NotAuthenticated();
                return null;
            }

            try
            {
                string url = string.Format(UrlGameDetails, prod_dev_namespace, prod_slugged_name);
                string response = Web.DownloadStringData(url, GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                ApiGameDetails data = null;
                if (!response.IsNullOrEmpty() && !response.Contains("\"product_data\": 404") && !Serialization.TryFromJson(response, out data))
                {
                    Logger.Warn($"GetGameDetails() - {response}");
                }
                return data;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
            }

            return null;
        }

        #region SearchData
        public List<SearchResult> SearchGame(string gameName)
        {
            List<SearchResult> all = new List<SearchResult>();
            List<SearchResult> searchStore = SearchStore(gameName);
            List<SearchResult> searchShowcase = SearchShowcase(gameName);
            all = all.Concat(searchStore).Concat(searchShowcase).Distinct().ToList();
            return all;
        }

        public List<SearchResult> SearchStore(string gameName)
        {
            List<SearchResult> searchResults = new List<SearchResult>();
            try
            {
                string csrf = GetCsrf();
                List<KeyValuePair<string, string>> moreHeader = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string> ("x-csrf-token", csrf),
                    new KeyValuePair<string, string> ("x-csrftoken", csrf)
                };
                string payload = "{\"input_string\": \"" + gameName + "\"}";
                string response = Web.PostStringDataPayload(UrlSearch, payload, GetWebCookies(), moreHeader).GetAwaiter().GetResult().Replace(Environment.NewLine, string.Empty);
                SearchResponse searchResponse = NormalizeResponseSearch(response);

                if (searchResponse != null && !searchResponse.Html.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    IHtmlDocument htmlDocument = parser.Parse(searchResponse.Html.Replace("\\", string.Empty));

                    foreach (IElement el in htmlDocument.QuerySelectorAll("ul.result-section li"))
                    {
                        if (el.GetAttribute("class").IsNullOrEmpty() || (!el.GetAttribute("class").Contains("results-top") && !el.GetAttribute("class").Contains("view-more")))
                        {
                            IElement figure = el.QuerySelector("figure");
                            IElement title = el.QuerySelector("div.title");
                            IElement price = el.QuerySelector("div.price");

                            if (figure != null && title != null)
                            {
                                searchResults.Add(new SearchResult
                                {
                                    Name = WebUtility.HtmlDecode(title.QuerySelector("a").InnerHtml.Replace("<span class=\"search-match\">", string.Empty).Replace("</span>", string.Empty)),
                                    ImageUrl = figure.QuerySelector("img").GetAttribute("src"),
                                    StoreUrl = UrlBase + figure.QuerySelector("a").GetAttribute("href"),
                                    IsShowcase = price?.InnerHtml.Contains("showcase", StringComparison.InvariantCultureIgnoreCase) ?? false
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
                Common.LogError(ex, false, true, "Indiegala");
            }

            return searchResults;
        }

        public List<SearchResult> SearchShowcase(string gameName)
        {
            List<SearchResult> searchResults = new List<SearchResult>();
            try
            {
                List<UserCollection> userCollections = GetUserShowcase();
                searchResults = userCollections?.Where(x => x.ProdName.IndexOf(gameName, StringComparison.InvariantCultureIgnoreCase) > -1)
                    ?.Select(x => new SearchResult
                    {
                        Name = x.ProdName,
                        IsShowcase = true,
                        ImageUrl = !x.ProdDevImage.IsNullOrEmpty()
                            ? string.Format(UrlProdMain, x.ProdDevNamespace, x.ProdIdKeyName, x.ProdDevImage)
                            : string.Empty,
                        StoreUrl = "https://" + x.ProdDevNamespace + ".indiegala.com/" + x.ProdSluggedName
                    })
                    ?.ToList() ?? new List<SearchResult>();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
            }

            return searchResults;
        }

        private static SearchResponse NormalizeResponseSearch(string responseSearch)
        {
            if (!responseSearch.IsNullOrEmpty())
            {
                responseSearch = responseSearch.Replace(Environment.NewLine, string.Empty);
                responseSearch = Regex.Replace(responseSearch, @"\r\n?|\n", string.Empty);

                string start = responseSearch.Substring(0, responseSearch.IndexOf("\"html\": \"") + 9);
                string end = "\"}";

                responseSearch = responseSearch.Replace(start, string.Empty).Replace(end, string.Empty);
                responseSearch = responseSearch.Replace("\"", "\\\"").Replace("\\\\", "\\");
                responseSearch = start + responseSearch.Replace("\"", "\\\"").Replace("\\\\", "\\") + end;

                if (!Serialization.TryFromJson(responseSearch, out SearchResponse searchResponse))
                {
                    Logger.Warn($"searchResponse: {Serialization.ToJson(searchResponse)}");
                }
                return searchResponse;
            }
            return null;
        }
        #endregion

        #region Indiegala
        private void LoginWithClient()
        {
            Logger.Info("LoginWithClient()");
            ResetIsUserLoggedIn();
            IndiegalaLibrary.IndiegalaClient.Open();
        }

        private void LoginWithoutClient()
        {
            Logger.Info("LoginWithoutClient()");
            ResetIsUserLoggedIn();

            WebViewSettings webViewSettings = new WebViewSettings
            {
                WindowWidth = 580,
                WindowHeight = 700,
                // This is needed otherwise captcha won't pass
                UserAgent = Web.UserAgent
            };

            using (IWebView webView = API.Instance.WebViews.CreateView(webViewSettings))
            {
                webView.LoadingChanged += (s, e) =>
                {
                    Common.LogDebug(true, $"NavigationChanged - {webView.GetCurrentAddress()}");
                    if (webView.GetCurrentAddress().StartsWith("https://www.indiegala.com", StringComparison.InvariantCultureIgnoreCase) && webView.GetCurrentAddress().IndexOf(LoginUrl) == -1 && webView.GetCurrentAddress().IndexOf(LogoutUrl) == -1)
                    {
                        IsUserLoggedIn = true;
                        webView.Close();
                    }
                };

                _isUserLoggedIn = false;
                webView.DeleteDomainCookies("www.indiegala.com");
                webView.DeleteDomainCookies(".indiegala.com");
                webView.Navigate(LoginUrl);
                _ = webView.OpenDialog();
            }

            if (IsUserLoggedIn)
            {
                _ = SetStoredCookies(GetWebCookies());
            }
        }

        private static string GetCsrf(string webData = null)
        {
            try
            {
                if (webData.IsNullOrEmpty())
                {
                    WebViewSettings webViewSettings = new WebViewSettings { UserAgent = Web.UserAgent };
                    using (IWebView webViews = API.Instance.WebViews.CreateOffscreenView(webViewSettings))
                    {
                        webViews.NavigateAndWait(UrlBase);
                        webData = webViews.GetPageSource();
                    }
                }
                string csrf = Regex.Match(webData, @"<input [ ]?name=""csrfmiddlewaretoken""[ ]?type=""hidden""[ ]?value=""(.*)""]?")?.Groups[1]?.Value;
                if (csrf.IsNullOrEmpty())
                {
                    Logger.Warn($"No csrf found");
                }
                else
                {
                    return csrf;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error in download library");
            }
            return string.Empty;
        }

        /*
        public ConnectionState GetConnectionState()
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
        */
        #endregion

        #region Errors
        public void NotAuthenticated()
        {
            _isUserLoggedIn = false;
            Logger.Warn("User is not authenticated");
            API.Instance.Notifications.Add(new NotificationMessage(
                "Indiegala-Error-UserCollections",
                "Indiegala" + Environment.NewLine + ResourceProvider.GetString("LOCCommonNotLoggedIn"),
                NotificationType.Error,
                () =>
                {
                    try
                    {
                        _ = API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == PlayniteTools.GetPluginId(PlayniteTools.ExternalPlugin.IndiegalaLibrary)).OpenSettingsView();
                    }
                    catch { }
                }));
        }
        #endregion
    }
}
