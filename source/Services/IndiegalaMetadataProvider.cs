using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using IndiegalaLibrary.Models;
using IndiegalaLibrary.Models.GalaClient;
using IndiegalaLibrary.Views;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;

namespace IndiegalaLibrary.Services
{
    /// <summary>
    /// Provides metadata extraction and transformation for Indiegala store and showcase pages.
    /// Implements Playnite's <see cref="LibraryMetadataProvider"/> to supply cover, background,
    /// description, genres, features and other metadata for games discovered via Indiegala.
    /// </summary>
    public class IndiegalaMetadataProvider : LibraryMetadataProvider
    {
        private static ILogger Logger => LogManager.GetLogger();
        private static IndiegalaApi IndiegalaApi => IndiegalaLibrary.IndiegalaApi;

        /// <summary>
        /// Reference to the plugin instance that owns this provider.
        /// </summary>
        private IndiegalaLibrary Plugin { get; }
        /// <summary>
        /// Plugin settings used to control image selection and other behaviours.
        /// </summary>
        private IndiegalaLibrarySettings Settings { get; }

        /// <summary>
        /// Maximum height used when resizing cover images (pixels).
        /// </summary>
        private int MaxHeight => 400;
        /// <summary>
        /// Maximum width used when resizing cover images (pixels).
        /// </summary>
        private int MaxWidth => 400;


        /// <summary>
        /// Creates a new instance of <see cref="IndiegalaMetadataProvider"/>.
        /// </summary>
        /// <param name="plugin">Instance of the <see cref="IndiegalaLibrary"/> plugin.</param>
        /// <param name="settings">Current plugin settings.</param>
        public IndiegalaMetadataProvider(IndiegalaLibrary plugin, IndiegalaLibrarySettings settings)
        {
            Plugin = plugin;
            Settings = settings;
        }


        /// <summary>
        /// Presents a dialog to the user to choose a background image from a list of candidate URLs.
        /// Returns an <see cref="ImageFileOption"/> describing the chosen image or a sentinel item
        /// with Path = "nopath" when no valid selection is available.
        /// </summary>
        /// <param name="possibleBackground">List of candidate background image URLs.</param>
        /// <returns>Selected <see cref="ImageFileOption"/> or a sentinel "nopath".</returns>
        private ImageFileOption GetBackgroundManually(List<string> possibleBackground)
        {
            List<ImageFileOption> selection = possibleBackground?.Select(x => new ImageFileOption { Path = x })?.ToList() ?? new List<ImageFileOption>();
            return selection.Count > 0
                ? API.Instance.Dialogs.ChooseImageFile(selection, ResourceProvider.GetString("LOCSelectBackgroundTitle"))
                : new ImageFileOption("nopath");
        }


        /// <summary>
        /// Extracts metadata for the provided <paramref name="game"/>. If the game is present in the
        /// user's Indiegala showcase the showcase metadata is returned. Otherwise the method will
        /// attempt to find the store page (optionally showing a search dialog) and parse available
        /// metadata from the page.
        /// </summary>
        /// <param name="game">Playnite <see cref="Game"/> to get metadata for.</param>
        /// <returns>Populated <see cref="GameMetadata"/> instance (may be partially filled on errors).</returns>
        public override GameMetadata GetMetadata(Game game)
        {
            GameMetadata gameMetadata = new GameMetadata()
            {
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                Features = new HashSet<MetadataProperty>(),
                GameActions = new List<GameAction>()
            };

            // Get showcase game data
            UserCollection userCollection = IndiegalaLibrary.IndiegalaApi.GetShowcaseData(game.GameId);
            if (userCollection != null)
            {
                return IndiegalaLibrary.IndiegalaApi.GetGameMetadata(userCollection, true);
            }

            // Get from web (store and other)
            string urlGame = game.Links?.FirstOrDefault(x => x.Name.IsEqual(ResourceProvider.GetString("LOCMetaSourceStore")))?.Url;
            bool getWithSelection = IndiegalaLibrary.IsLibrary ? urlGame.IsNullOrEmpty() : urlGame.IsNullOrEmpty() || !Settings.SelectOnlyWithoutStoreUrl;
            if (getWithSelection)
            {
                if (Settings.UseMatchValue)
                {
                    List<SearchResult> dataSearch = new List<SearchResult>();
                    try
                    {
                        dataSearch = IndiegalaApi.SearchGame(game.Name);
                        var top = dataSearch.FirstOrDefault();
                        if (top != null && top.MatchPercent >= Settings.MatchValue)
                        {
                            urlGame = top.StoreUrl;
                            gameMetadata.Links.Add(new Link { Name = ResourceProvider.GetString("LOCMetaSourceStore"), Url = urlGame });
                        }
                        else
                        {
                            Logger.Warn($"No match >= {Settings.MatchValue} for {game.Name}");
                            return gameMetadata;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                        return gameMetadata;
                    }
                }
                else
                {
                    // Search game
                    IndiegalaLibrarySearch ViewExtension = null;
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        ViewExtension = new IndiegalaLibrarySearch(game.Name);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCMetaLookupWindowTitle"), ViewExtension);
                        _ = windowExtension.ShowDialog();
                    }));

                    if (!ViewExtension.DataResponse.Name.IsNullOrEmpty())
                    {
                        urlGame = ViewExtension.DataResponse.StoreUrl;
                        gameMetadata.Links.Add(new Link { Name = ResourceProvider.GetString("LOCMetaSourceStore"), Url = urlGame });
                    }
                    else
                    {
                        Logger.Warn($"No url for {game.Name}");
                        return gameMetadata;
                    }
                }
            }

            if (urlGame.IsNullOrEmpty())
            {
                Common.LogDebug(true, $"No url for {game.Name}");
                return gameMetadata;
            }

            Common.LogDebug(true, $"urlGame: {urlGame}");

            string response = string.Empty;
            using (IWebView webView = API.Instance.WebViews.CreateOffscreenView())
            {
                webView.NavigateAndWait(urlGame);
                response = webView.GetPageSource();
            }

            if (!response.IsNullOrEmpty())
            {
                if (response.Contains("request unsuccessful", StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Error($"Request unsuccessful for {urlGame}");
                    _ = API.Instance.Dialogs.ShowErrorMessage($"Request unsuccessful for {urlGame}", "IndiegalaLibrary");

                    return gameMetadata;
                }
                if (response.Contains("<body></body>", StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Error($"Request with no data for {urlGame}");
                    _ = API.Instance.Dialogs.ShowErrorMessage($"Request with no data for {urlGame}", "IndiegalaLibrary");

                    return gameMetadata;
                }

                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(response);

                if (htmlDocument.QuerySelector("h1.developer-product-title") != null)
                {
                    gameMetadata = ParseType1(htmlDocument, gameMetadata);
                    if (!gameMetadata.Links.Any(l => l.Url == urlGame))
                    {
                        gameMetadata.Links.Add(new Link { Name = ResourceProvider.GetString("LOCMetaSourceStore"), Url = urlGame });
                    }
                }
                else if (htmlDocument.QuerySelector("h1.store-product-page-title") != null)
                {
                    gameMetadata = ParseType2(htmlDocument, gameMetadata);
                    if (!gameMetadata.Links.Any(l => l.Url == urlGame))
                    {
                        gameMetadata.Links.Add(new Link { Name = ResourceProvider.GetString("LOCMetaSourceStore"), Url = urlGame });
                    }
                }
                else if (response.Contains("404 - Page not found", StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Warn($"Page not found for {urlGame}");
                }
                else
                {
                    Logger.Warn($"No parser for {urlGame}");
                }
            }

            Common.LogDebug(true, $"metadata: {Serialization.ToJson(gameMetadata)}");
            return gameMetadata;
        }


        /// <summary>
        /// Parses metadata from developer-style product pages.
        /// Extracts cover, background images, description, links, developer/publisher, release date,
        /// categories and specs (features).
        /// </summary>
        /// <param name="htmlDocument">Parsed HTML document of the product page.</param>
        /// <param name="gameMetadata">Existing metadata instance to populate.</param>
        /// <returns>Populated <see cref="GameMetadata"/>.</returns>
        private GameMetadata ParseType1(IHtmlDocument htmlDocument, GameMetadata gameMetadata)
        {
            // Cover Image
            try
            {
                string coverImage = htmlDocument.QuerySelector("figure.developer-product-cover img")?.GetAttribute("src");
                if (coverImage.IsNullOrEmpty())
                {
                    coverImage = htmlDocument.QuerySelector("figure.developer-product-cover img")?.GetAttribute("data-img-src");
                }
                if (!coverImage.IsNullOrEmpty())
                {
                    gameMetadata.CoverImage = ResizeCoverImage(new MetadataFile(coverImage));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on CoverImage");
            }

            //Background Image
            try
            {
                List<string> possibleBackgrounds = new List<string>();
                foreach (IElement searchElement in htmlDocument.QuerySelectorAll("div.developer-product-media-col img"))
                {
                    string imgSrc = searchElement.GetAttribute("src");
                    if (imgSrc.IsNullOrEmpty())
                    {
                        imgSrc = searchElement.GetAttribute("data-img-src");
                    }

                    if (imgSrc.IndexOf("indiegala") > -1)
                    {
                        possibleBackgrounds.Add(imgSrc);
                    }
                }
                if (possibleBackgrounds.Count > 0)
                {
                    // Selection mode
                    IndiegalaLibrarySettings settings = Plugin.LoadPluginSettings<IndiegalaLibrarySettings>();
                    Common.LogDebug(true, $"ImageSelectionPriority: {settings.ImageSelectionPriority}");

                    if (settings.ImageSelectionPriority == 0)
                    {
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && API.Instance.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        int index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        ImageFileOption selection = GetBackgroundManually(possibleBackgrounds);
                        if (selection != null && selection.Path != "nopath")
                        {
                            gameMetadata.BackgroundImage = new MetadataFile(selection.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on BackgroundImage");
            }


            //Description 
            try
            {
                foreach (IElement SearchElement in htmlDocument.QuerySelectorAll("div.developer-product-description"))
                {
                    if (!SearchElement.GetAttribute("class").Contains("display"))
                    {
                        string Description = SearchElement.InnerHtml.Trim();
                        gameMetadata.Description = Description;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on Description");
            }

            // Link 
            foreach (IElement el in htmlDocument.QuerySelectorAll("div.developer-product-contacts li"))
            {
                switch (el.QuerySelector("i").GetAttribute("class").ToLower())
                {
                    case "fa fa-globe":
                        gameMetadata.Links.Add(new Link { Name = ResourceProvider.GetString("LOCWebsiteLabel"), Url = el.QuerySelector("a").GetAttribute("href") });
                        break;

                    case "fa fa-facebook-official":
                        gameMetadata.Links.Add(new Link { Name = "Facebook", Url = el.QuerySelector("a").GetAttribute("href") });
                        break;

                    case "fa fa-twitter":
                        gameMetadata.Links.Add(new Link { Name = "Twitter", Url = el.QuerySelector("a").GetAttribute("href") });
                        break;

                    default:
                        break;
                }
            }

            // More
            try
            {
                IElement developerProduct = htmlDocument.QuerySelectorAll("div.developer-product-contents-aside-inner").First();
                foreach (IElement SearchElement in developerProduct.QuerySelectorAll("div.developer-product-contents-aside-block"))
                {
                    switch (SearchElement.QuerySelector("div.developer-product-contents-aside-title").InnerHtml.ToLower())
                    {
                        case "published":
                            string strReleased = SearchElement.QuerySelector("div.developer-product-contents-aside-text").InnerHtml;
                            Common.LogDebug(true, $"strReleased: {strReleased}");

                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                            {
                                gameMetadata.ReleaseDate = new ReleaseDate(dateTime);
                            }
                            break;

                        case "categories":
                            foreach (IElement Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                HashSet<MetadataProperty> Genres = gameMetadata.Genres;
                                foreach (Genre genre in API.Instance.Database.Genres)
                                {
                                    if (genre.Name.ToLower() == strCategories.ToLower())
                                    {
                                        Genres.Add(new MetadataNameProperty(genre.Name));
                                    }
                                }
                                gameMetadata.Genres = Genres;
                            }
                            break;

                        case "specs":
                            foreach (IElement Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strModes = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
                                Common.LogDebug(true, $"strModes: {strModes}");

                                HashSet<MetadataProperty> Features = gameMetadata.Features;
                                if (strModes.ToLower() == "single-player")
                                {
                                    Features.Add(new MetadataNameProperty("Single Player"));
                                }
                                if (strModes.ToLower() == "full controller support")
                                {
                                    Features.Add(new MetadataNameProperty("Full Controller Support"));
                                }
                                gameMetadata.Features = Features;
                            }
                            break;

                        default:
                            break;
                    }
                }

                string strDeveloper = htmlDocument.QuerySelector("h2.developer-product-subtitle a").InnerHtml.Trim();
                if (!strDeveloper.IsEqual("galaFreebies"))
                {
                    gameMetadata.Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(strDeveloper) };
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GameDetails");
            }

            return gameMetadata;
        }

        /// <summary>
        /// Parses metadata from store-style product pages.
        /// Extracts cover, background images, description, publisher, developer, release date,
        /// categories and modes (features).
        /// </summary>
        /// <param name="htmlDocument">Parsed HTML document of the product page.</param>
        /// <param name="gameMetadata">Existing metadata instance to populate.</param>
        /// <returns>Populated <see cref="GameMetadata"/>.</returns>
        private GameMetadata ParseType2(IHtmlDocument htmlDocument, GameMetadata gameMetadata)
        {
            // Cover Image
            try
            {
                string coverImage = htmlDocument.QuerySelector("div.main-info-box-resp img.img-fit")?.GetAttribute("src");
                if (!coverImage.IsNullOrEmpty())
                {
                    gameMetadata.CoverImage = ResizeCoverImage(new MetadataFile(coverImage));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on CoverImage");
            }

            //Background Image
            try
            {
                List<string> possibleBackgrounds = new List<string>();
                foreach (IElement SearchElement in htmlDocument.QuerySelectorAll("div.media-caption-small img"))
                {
                    if (SearchElement.GetAttribute("src").IndexOf("indiegala") > -1)
                    {
                        possibleBackgrounds.Add(SearchElement.GetAttribute("src"));
                    }
                }
                if (possibleBackgrounds.Count > 0)
                {
                    // Selection mode
                    IndiegalaLibrarySettings settings = Plugin.LoadPluginSettings<IndiegalaLibrarySettings>();
                    Common.LogDebug(true, $"ImageSelectionPriority: {settings.ImageSelectionPriority}");

                    if (settings.ImageSelectionPriority == 0)
                    {
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && API.Instance.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        int index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        ImageFileOption selection = GetBackgroundManually(possibleBackgrounds);
                        if (selection != null && selection.Path != "nopath")
                        {
                            gameMetadata.BackgroundImage = new MetadataFile(selection.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on BackgroundImage");
            }

            //Description 
            try
            {
                string Description = htmlDocument.QuerySelector("section.description-cont div.description div.description-inner").InnerHtml;
                gameMetadata.Description = Description.Trim();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on Description");
            }

            // More
            try
            {
                foreach (IElement SearchElement in htmlDocument.QuerySelectorAll("section.store-product-sub-info-box-resp div.info-row"))
                {
                    switch (SearchElement.QuerySelector("div.info-title").InnerHtml.ToLower())
                    {
                        case "publisher":
                            string strPublisher = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont a").InnerHtml).Trim();
                            Common.LogDebug(true, $"strPublisher: {strPublisher}");

                            if (!strPublisher.IsEqual("galaFreebies"))
                            {
                                gameMetadata.Publishers = new HashSet<MetadataProperty> { new MetadataNameProperty(strPublisher) };
                            }
                            break;

                        case "developer":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont").InnerHtml).Trim();
                            Common.LogDebug(true, $"strDevelopers: {strDevelopers}");

                            if (!strDevelopers.IsEqual("galaFreebies"))
                            {
                                gameMetadata.Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(strDevelopers) };
                            }
                            break;

                        case "released":
                            string strReleased = SearchElement.QuerySelector("div.info-cont").InnerHtml.Trim();
                            Common.LogDebug(true, $"strReleased: {strReleased}");

                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                            {
                                gameMetadata.ReleaseDate = new ReleaseDate(dateTime);
                            }
                            break;

                        case "categories":
                            foreach (IElement Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span a"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml);
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                HashSet<MetadataProperty> Genres = gameMetadata.Genres;
                                foreach (var genre in API.Instance.Database.Genres)
                                {
                                    if (genre.Name.ToLower() == strCategories.ToLower())
                                    {
                                        Genres.Add(new MetadataNameProperty(genre.Name));
                                    }
                                }
                                gameMetadata.Genres = Genres;
                            }
                            break;

                        case "modes":
                            foreach (IElement Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span"))
                            {
                                string strModes = Element.InnerHtml;
                                Common.LogDebug(true, $"strModes: {strModes}");

                                HashSet<MetadataProperty> Features = gameMetadata.Features;
                                if (strModes.ToLower() == "single-player")
                                {
                                    Features.Add(new MetadataNameProperty("Single Player"));
                                }
                                if (strModes.ToLower() == "full controller support")
                                {
                                    Features.Add(new MetadataNameProperty("Full Controller Support"));
                                }
                                gameMetadata.Features = Features;
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GameDetails");
            }

            return gameMetadata;
        }


        /// <summary>
        /// Downloads and resizes a cover image to fit within the configured max width/height while
        /// preserving aspect ratio. Returns a new <see cref="MetadataFile"/> containing the resized
        /// image bytes when successful; otherwise returns the original metadata file.
        /// </summary>
        /// <param name="originalMetadataFile">Original cover metadata referencing a remote image URL.</param>
        /// <returns>Resized <see cref="MetadataFile"/> or the original on failure.</returns>
        private MetadataFile ResizeCoverImage(MetadataFile originalMetadataFile)
        {
            MetadataFile metadataFile = originalMetadataFile;

            try
            {
                Stream imageStream = Web.DownloadFileStream(originalMetadataFile.Path).GetAwaiter().GetResult();
                ImageProperty imageProperty = ImageTools.GetImapeProperty(imageStream);

                string fileName = Path.GetFileNameWithoutExtension(originalMetadataFile.FileName);

                if (imageProperty != null)
                {
                    string newCoverPath = Path.Combine(PlaynitePaths.ImagesCachePath, fileName);

                    if (imageProperty.Width <= imageProperty.Height)
                    {
                        int newWidth = imageProperty.Width * MaxHeight / imageProperty.Height;
                        Common.LogDebug(true, $"FileName: {fileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewWidth: {newWidth}");

                        ImageTools.Resize(imageStream, newWidth, MaxHeight, newCoverPath);
                    }
                    else
                    {
                        int newHeight = imageProperty.Height * MaxWidth / imageProperty.Width;
                        Common.LogDebug(true, $"FileName: {fileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewHeight: {newHeight}");

                        ImageTools.Resize(imageStream, MaxWidth, newHeight, newCoverPath);
                    }

                    Common.LogDebug(true, $"NewCoverPath: {newCoverPath}.png");

                    if (File.Exists(newCoverPath + ".png"))
                    {
                        Common.LogDebug(true, $"Used new image size");
                        metadataFile = new MetadataFile(fileName, File.ReadAllBytes(newCoverPath + ".png"));
                    }
                    else
                    {
                        Common.LogDebug(true, $"Used OriginalUrl");
                        metadataFile = new MetadataFile(fileName, File.ReadAllBytes(newCoverPath + ".png"));
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on resize CoverImage from {originalMetadataFile.Path}");
            }

            return metadataFile;
        }
    }
}