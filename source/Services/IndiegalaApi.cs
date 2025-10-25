using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Extensions;
using FuzzySharp;
using IndiegalaLibrary.Models;
using IndiegalaLibrary.Models.Api;
using IndiegalaLibrary.Models.GalaClient;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace IndiegalaLibrary.Services
{
    public enum DataType { bundle, store }

    public enum ConnectionState { Logged, Locked, Unlogged }


    /// <summary>
    /// Provides methods to interact with the Indiegala platform, including authentication,
    /// retrieving owned games, searching for games, and managing user data and cookies.
    /// </summary>
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

        /// <summary>
        /// Indicates whether the user is currently logged in to Indiegala.
        /// </summary>
        protected bool? _isUserLoggedIn;

        /// <summary>
        /// Gets or sets the login state of the user.
        /// </summary>
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

        /// <summary>
        /// Tool for managing cookies
        /// </summary>
        protected CookiesTools CookiesTools { get; }

        /// <summary>
        /// List of domains for which cookies are managed.
        /// </summary>
        protected List<string> CookiesDomains { get; }

        /// <summary>
        /// Path to the file where cookies are stored.
        /// </summary>
        protected string FileCookies { get; }

        /// <summary>
        /// Path to the cache data directory.
        /// </summary>
        protected string PathCacheData { get; }

        /// <summary>
        /// Path to the plugin user data directory.
        /// </summary>
        protected string PluginUserDataPath { get; }

        /// <summary>
        /// Indicates whether the Indiegala client should be used for authentication.
        /// </summary>
        private bool UseClient { get; }


        private static Regex BundleResponseRegex => new Regex(@"^\{\s*""status"":\s*""(?<status>\w+)"",\s*""code"":\s*""(?<code>\w*)"",\s*""html"":\s*""(?<html>.*)""}$", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);


        /// <summary>
        /// Initializes a new instance of the <see cref="IndiegalaApi"/> class.
        /// </summary>
        /// <param name="pluginUserDataPath">Path to the plugin user data directory.</param>
        /// <param name="useClient">Indicates whether to use the Indiegala client for authentication.</param>
        public IndiegalaApi(string pluginUserDataPath, bool useClient)
        {
            PathCacheData = Path.Combine(PlaynitePaths.DataCachePath, "Indiegala");
            PluginUserDataPath = pluginUserDataPath;
            UseClient = useClient;

            CookiesDomains = new List<string> { "www.indiegala.com", ".indiegala.com" };
            FileCookies = Path.Combine(pluginUserDataPath, CommonPlayniteShared.Common.Paths.GetSafePathName($"Indiegala_Cookies.dat"));
            CookiesTools = new CookiesTools(
                "Indiegala",
                "Indiegala",
                FileCookies,
                CookiesDomains
            );
        }

        #region Configuration

        /// <summary>
        /// Resets the login state, forcing a re-check on the next access.
        /// </summary>
        public void ResetIsUserLoggedIn()
        {
            _isUserLoggedIn = null;
        }

        /// <summary>
        /// Checks if the user is currently logged in to Indiegala.
        /// </summary>
        /// <returns>True if logged in, otherwise false.</returns>
        protected bool GetIsUserLoggedIn()
        {
            try
            {
                string response = Web.DownloadStringData(UrlUserInfo, CookiesTools.GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                return !response.IsNullOrEmpty() && response.Contains("\"user_found\": \"true\"");
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Initiates the login process for the user.
        /// </summary>
        public void Login()
        {
            LoginWithoutClient();
        }

        #endregion

        #region Games Showcase

        /// <summary>
        /// Retrieves the user's showcase collections from Indiegala.
        /// </summary>
        /// <returns>List of user collections.</returns>
        private List<UserCollection> GetUserShowcase()
        {
            if (!IsUserLoggedIn)
            {
                NotAuthenticated();
                return new List<UserCollection>();
            }

            try
            {
                string cachePath = Path.Combine(PathCacheData, $"UrlUserInfo.json");
                List<UserCollection> userCollections = LoadData<List<UserCollection>>(cachePath, 10);

                if (userCollections == null)
                {
                    string response = Web.DownloadStringData(UrlUserInfo, CookiesTools.GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                    if (!response.IsNullOrEmpty() && response.Contains("\"user_found\": \"true\""))
                    {
                        dynamic data = Serialization.FromJson<dynamic>(response);
                        string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                        _ = Serialization.TryFromJson(userCollectionString, out userCollections);
                        SaveData(cachePath, userCollections);
                    }
                }

                return userCollections;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            NotAuthenticated();
            return new List<UserCollection>();
        }

        /// <summary>
        /// Gets the list of owned games from the user's showcase.
        /// </summary>
        /// <param name="withDetails">Whether to include detailed information for each game.</param>
        /// <returns>List of owned games metadata.</returns>
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

        /// <summary>
        /// Retrieves detailed information for a specific game in the showcase.
        /// </summary>
        /// <param name="gameMetadata">Metadata of the game.</param>
        /// <param name="version">Version of the game.</param>
        /// <param name="prod_dev_namespace">Developer namespace.</param>
        /// <param name="prod_slugged_name">Slugged name of the product.</param>
        /// <returns>Updated game metadata with details.</returns>
        public GameMetadata GetShowCaseDetails(GameMetadata gameMetadata, int version, string prod_dev_namespace, string prod_slugged_name)
        {
            try
            {
                string cachePath = Path.Combine(PathCacheData, $"{prod_slugged_name}_{version}.json");
                ApiGameDetails data = LoadData<ApiGameDetails>(cachePath, -1);

                if (data == null)
                {
                    data = GetGameDetails(prod_dev_namespace, prod_slugged_name);
                    SaveData(cachePath, data);
                }

                List<GameAction> gameActions = new List<GameAction>();
                if (!(data?.ProductData.DownloadableVersions?.Win?.IsNullOrEmpty() ?? false))
                {
                    GameAction downloadAction = new GameAction()
                    {
                        Name = ResourceProvider.GetString("LOCDownloadLabel") ?? "Download",
                        Type = GameActionType.URL,
                        Path = data?.ProductData?.DownloadableVersions?.Win
                    };
                    gameActions.Add(downloadAction);
                };

                int? communityScore = null;
                if (data?.ProductData?.Rating?.AvgRating != null)
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

        /// <summary>
        /// Builds game metadata from a user collection entry.
        /// </summary>
        /// <param name="userCollection">User collection entry.</param>
        /// <param name="withDetails">Whether to include detailed information.</param>
        /// <returns>Game metadata object.</returns>
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
                    gameMetadata = GetShowCaseDetails(gameMetadata, userCollection.Version?.Max(x => x.Id) ?? 0, userCollection.ProdDevNamespace, userCollection.ProdSluggedName);
                }

                return gameMetadata;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
                return null;
            }
        }

        /// <summary>
        /// Gets showcase data for a specific game by its ID.
        /// </summary>
        /// <param name="gameId">Game ID.</param>
        /// <returns>User collection entry for the game.</returns>
        public UserCollection GetShowcaseData(string gameId)
        {
            return GetUserShowcase()?.Where(x => x.ProdIdKeyName.ToString().IsEqual(gameId))?.FirstOrDefault() ?? null;
        }

        #endregion

        #region Games Bundle or Store

        /// <summary>
        /// Retrieves owned games from bundles or store, depending on the specified data type.
        /// </summary>
        /// <param name="dataType">Type of data to retrieve (bundle or store).</param>
        /// <returns>List of owned games metadata.</returns>
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
                    var data = Web.DownloadSourceDataWebView(url, CookiesTools.GetStoredCookies()).GetAwaiter().GetResult();
                    string webData = data.Item1;

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

                                string response = Web.PostStringDataPayload(urlData, payload, CookiesTools.GetStoredCookies(), moreHeader).GetAwaiter().GetResult();
                                StoreBundleResponse storeBundleResponse = ParseBundleResponse(response);
                                if (!storeBundleResponse?.Status?.IsEqual("ok") ?? true)
                                {
                                    Logger.Warn($"No data for {dataOrigin} - {id}");
                                    continue;
                                }

                                parser = new HtmlParser();
                                htmlDocument = parser.Parse(storeBundleResponse.Html);

                                List<StoreData> storeDatas = GetStoreData(dataType, storeBundleResponse.Html).ToList();
                                foreach (StoreData storeData in storeDatas)
                                {
                                    if (storeData.Type.IsEqual("STEAM"))
                                    {
                                        Logger.Info($"{storeData.Name} is not a Indiegala game in {dataOrigin}");
                                        continue;
                                    }

                                    GameMetadata gameMetadata = new GameMetadata()
                                    {
                                        Source = new MetadataNameProperty("Indiegala"),
                                        GameId = storeData.Name.GetSHA256Hash(),
                                        Name = storeData.Name,
                                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                                        GameActions = new List<GameAction>(),
                                        Links = new List<Link>(),
                                        CoverImage = !storeData.Image2.IsNullOrEmpty() ? new MetadataFile(storeData.Image2) : null
                                    };

                                    if (!storeData.DownloadUrl.IsNullOrEmpty())
                                    {
                                        gameMetadata.GameActions.Add(new GameAction
                                        {
                                            Name = ResourceProvider.GetString("LOCDownloadLabel") ?? "Download",
                                            Type = GameActionType.URL,
                                            Path = storeData.DownloadUrl,
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
        /// Parses the bundle response from Indiegala.
        /// </summary>
        /// <param name="content">Response content.</param>
        /// <returns>Parsed bundle response object.</returns>
        private static StoreBundleResponse ParseBundleResponse(string content)
        {
            Match match = BundleResponseRegex.Match(content);
            if (match.Success)
            {
                return new StoreBundleResponse
                {
                    Code = match.Groups["code"].Value,
                    Status = match.Groups["status"].Value,
                    Html = match.Groups["html"].Value.Replace("\\\"", "\"")
                };
            }
            return Serialization.FromJson<StoreBundleResponse>(content);
        }

        /// <summary>
        /// Extracts store data from HTML content.
        /// </summary>
        /// <param name="dataType">Type of data (bundle or store).</param>
        /// <param name="html">HTML content.</param>
        /// <returns>Enumerable of store data objects.</returns>
        private static IEnumerable<StoreData> GetStoreData(DataType dataType, string html)
        {
            HtmlParser parser = new HtmlParser();
            IHtmlDocument htmlDocument = parser.Parse(html);

            foreach (IElement listItem in htmlDocument.QuerySelectorAll("li.profile-private-page-library-subitem"))
            {
                StoreData storeData = dataType == DataType.bundle ? ParseListItemBundle(listItem) : ParseListItemStore(listItem);
                if (storeData != null)
                {
                    yield return storeData;
                }
            }
        }

        /// <summary>
        /// Parses a bundle list item from HTML to a StoreData object.
        /// </summary>
        /// <param name="listItem">HTML element representing the list item.</param>
        /// <returns>Parsed StoreData object.</returns>
        private static StoreData ParseListItemBundle(IElement listItem)
        {
            var pre = listItem.QuerySelector("pre.display-none");
            if (pre == null) { return null; }
            var data = pre.InnerHtml
                .Replace("bundle_item: ", string.Empty)
                .Replace("\\n", string.Empty)
                .Replace("\\t", string.Empty)
                .Replace('\'', '"')
                .Replace("None,", "\"\",");

            _ = Serialization.TryFromJson(data, out StoreData storeData, out Exception ex);
            if (ex != null)
            {
                Common.LogError(ex, false, $"Error parsing StoreData: {ex.Message}");
                return null;
            }

            return storeData;
        }

        /// <summary>
        /// Parses a store list item from HTML to a StoreData object.
        /// </summary>
        /// <param name="listItem">HTML element representing the list item.</param>
        /// <returns>Parsed StoreData object.</returns>
        private static StoreData ParseListItemStore(IElement listItem)
        {
            try
            {
                string name = listItem.QuerySelector("div.profile-private-page-library-title-row")?.InnerHtml ?? listItem.QuerySelector("div.profile-private-page-library-title-row-full")?.InnerHtml;
                string downloadUrl = listItem.QuerySelector("a.bg-gradient-light-blue")?.GetAttribute("href") ?? String.Empty;
                string coverUrl = listItem.QuerySelector("img.async-img-load").GetAttribute("data-src");
                string type = listItem.QuerySelector("input.profile-private-page-library-key-serial") != null ? "STEAM" : "INDIEGALA";

                return new StoreData
                {
                    Name = name,
                    DownloadUrl = downloadUrl,
                    Image2 = coverUrl,
                    Type = type
                };

            }
            catch(Exception ex)
            {
                Common.LogError(ex, false, $"Error parsing StoreData: {ex.Message}");
            }


            return null;
        }

        #endregion

        /// <summary>
        /// Retrieves detailed information for a game from Indiegala API.
        /// </summary>
        /// <param name="prod_dev_namespace">Developer namespace.</param>
        /// <param name="prod_slugged_name">Slugged name of the product.</param>
        /// <returns>Game details object.</returns>
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
                string response = Web.DownloadStringData(url, CookiesTools.GetStoredCookies(), "galaClient").GetAwaiter().GetResult();
                ApiGameDetails data = null;
                if (!response.IsNullOrEmpty() && !response.Contains("\"product_data\": 404") && !Serialization.TryFromJson(response, out data))
                {
                    Logger.Warn($"GetGameDetails({prod_dev_namespace}, {prod_slugged_name})");
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

        /// <summary>
        /// Searches for games by name across both store and showcase.
        /// </summary>
        /// <param name="gameName">Name of the game to search for.</param>
        /// <returns>List of search results.</returns>
        public List<SearchResult> SearchGame(string gameName)
        {
            List<SearchResult> all = new List<SearchResult>();
            List<SearchResult> searchStore = SearchStore(gameName);
            List<SearchResult> searchShowcase = SearchShowcase(gameName);
            
            all = all.Concat(searchStore).Concat(searchShowcase).Distinct().ToList();
            all = all.Select(x => new SearchResult
                {
                    MatchPercent = Fuzz.Ratio(gameName.ToLower(), x.Name.ToLower()),
                    Name = x.Name,
                    ImageUrl = x.ImageUrl,
                    StoreUrl = x.StoreUrl,
                    IsShowcase = x.IsShowcase
                })
                .OrderByDescending(x => x.MatchPercent)
                .ToList();

            return all;
        }

        /// <summary>
        /// Searches for games by name in the Indiegala store.
        /// </summary>
        /// <param name="gameName">Name of the game to search for.</param>
        /// <returns>List of search results.</returns>
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
                string response = Web.PostStringDataPayload(UrlSearch, payload, CookiesTools.GetWebCookies(), moreHeader).GetAwaiter().GetResult().Replace(Environment.NewLine, string.Empty);
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

        /// <summary>
        /// Searches for games by name in the user's showcase.
        /// </summary>
        /// <param name="gameName">Name of the game to search for.</param>
        /// <returns>List of search results.</returns>
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

        /// <summary>
        /// Normalizes the search response from Indiegala.
        /// </summary>
        /// <param name="responseSearch">Raw search response string.</param>
        /// <returns>Normalized search response object.</returns>
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

        /// <summary>
        /// Initiates login using the Indiegala client.
        /// </summary>
        private void LoginWithClient()
        {
            Logger.Info("LoginWithClient()");
            ResetIsUserLoggedIn();
            IndiegalaLibrary.IndiegalaClient.Open();
        }

        /// <summary>
        /// Initiates login using a web view (without the Indiegala client).
        /// </summary>
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
                
                foreach(var domain in CookiesDomains)
                {
                    webView.DeleteDomainCookies(domain);
                }

                webView.Navigate(LoginUrl);
                _ = webView.OpenDialog();
            }

            if (IsUserLoggedIn)
            {
                _ = CookiesTools.SetStoredCookies(CookiesTools.GetWebCookies());
            }
        }

        /// <summary>
        /// Retrieves the CSRF token from the web data or by loading the Indiegala homepage.
        /// </summary>
        /// <param name="webData">Optional HTML content to extract the token from.</param>
        /// <returns>CSRF token string.</returns>
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

        #endregion

        #region Errors

        /// <summary>
        /// Shows a notification to the user when not authenticated.
        /// </summary>
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
                        ResetIsUserLoggedIn();
                        _ = API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == PlayniteTools.GetPluginId(PlayniteTools.ExternalPlugin.IndiegalaLibrary)).OpenSettingsView();
                    }
                    catch { }
                }));
        }

        /// <summary>
        /// Shows a notification to the user about using old cached data.
        /// </summary>
        /// <param name="dateLastWrite">The date when the data was last updated</param>
        protected void ShowNotificationOldData(DateTime dateLastWrite)
        {
            LocalDateTimeConverter localDateTimeConverter = new LocalDateTimeConverter();
            string formatedDateLastWrite = localDateTimeConverter.Convert(dateLastWrite, null, null, CultureInfo.CurrentCulture).ToString();
            Logger.Warn($"Use saved UserData - {formatedDateLastWrite}");
            API.Instance.Notifications.Add(new NotificationMessage(
                $"Indiegala-Error-OldData",
                $"Indiegala" + Environment.NewLine
                    + string.Format(ResourceProvider.GetString("LOCCommonNotificationOldData"), "Indiegala", formatedDateLastWrite),
                NotificationType.Info,
                () =>
                {
                    try
                    {
                        ResetIsUserLoggedIn();
                        _ = API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == PlayniteTools.GetPluginId(PlayniteTools.ExternalPlugin.IndiegalaLibrary)).OpenSettingsView();
                    }
                    catch { }
                }
            ));
        }

        #endregion

        /// <summary>
        /// Loads data from a file, optionally checking for cache expiration.
        /// </summary>
        /// <typeparam name="T">Type of data to load.</typeparam>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="minutes">Cache expiration in minutes. If 0, always show notification.</param>
        /// <returns>Loaded data object, or null if not found or expired.</returns>
        protected T LoadData<T>(string filePath, int minutes) where T : class
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                DateTime dateLastWrite = File.GetLastWriteTime(filePath);

                if (minutes > 0 && dateLastWrite.AddMinutes(minutes) <= DateTime.Now)
                {
                    return null;
                }

                if (minutes == 0)
                {
                    ShowNotificationOldData(dateLastWrite);
                }

                return Serialization.FromJsonFile<T>(filePath);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
                return null;
            }
        }

        /// <summary>
        /// Saves data to a file.
        /// </summary>
        /// <typeparam name="T">Type of data to save.</typeparam>
        /// <param name="filePath">Path to the file.</param>
        /// <param name="data">Data to save.</param>
        /// <returns>True if successful, otherwise false.</returns>
        protected bool SaveData<T>(string filePath, T data) where T : class
        {
            try
            {
                if (data == null)
                {
                    return false;
                }

                FileSystem.PrepareSaveFile(filePath);
                if (data is string s)
                {
                    File.WriteAllText(filePath, s);
                }
                else
                {
                    File.WriteAllText(filePath, Serialization.ToJson(data));
                }
                return true;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "Indiegala");
                return false;
            }
        }
    }
}