using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using IndiegalaLibrary.Views;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using CommonPluginsShared;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows;
using CommonPluginsPlaynite;
using CommonPluginsPlaynite.Common;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaMetadataProvider : LibraryMetadataProvider
    {
        private ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private readonly IPlayniteAPI PlayniteApi;
        private readonly IndiegalaLibrary Plugin;
        private readonly IndiegalaLibrarySettings PluginSettings;

        private readonly int MaxHeight = 400;
        private readonly int MaxWidth = 400;


        public IndiegalaMetadataProvider(IndiegalaLibrary Plugin, IPlayniteAPI PlayniteApi, IndiegalaLibrarySettings PluginSettings)
        {
            this.PlayniteApi = PlayniteApi;
            this.Plugin = Plugin;
            this.PluginSettings = PluginSettings;
        }


        private ImageFileOption GetBackgroundManually(List<string> possibleBackground)
        {
            var selection = new List<ImageFileOption>();
            foreach (var backgroundUrl in possibleBackground)
            {
                selection.Add(new ImageFileOption { Path = backgroundUrl });
            }
            if (selection.Count > 0)
            {
                return PlayniteApi.Dialogs.ChooseImageFile(selection, resources.GetString("LOCSelectBackgroundTitle"));
            }
            else
            {
                return new ImageFileOption("nopath");
            }
        }


        public override GameMetadata GetMetadata(Game game)
        {
            // TODO Rewrite when find api request
            if (PluginSettings.UseClient)
            {
                var MetadataClient = IndiegalaAccountClient.GetMetadataWithClient(PlayniteApi, game.GameId);
                if (MetadataClient != null)
                {
                    return MetadataClient;
                }
            }


            var gameMetadata = new GameMetadata() {
                Links = new List<Link>(),
                Tags = new HashSet<MetadataProperty>(),
                Genres = new HashSet<MetadataProperty>(),
                Features = new HashSet<MetadataProperty>(),
                GameActions = new List<GameAction>()
            };


            string urlGame = string.Empty;
            List<Link> Links = new List<Link>();
            if (game.Links != null)
            {
                foreach (var Link in game.Links)
                {
                    if (Link.Name.ToLower() == "store" && Link.Url.ToLower().Contains("indiegala"))
                    {
                        urlGame = Link.Url;

                        if (game.Links.Count == 1)
                        {
                            game.Links = null;
                        }
                    }
                    Links.Add(Link);
                }
            }

            bool GetWithSelection = false;
            if (IndiegalaLibrary.IsLibrary)
            {
                GetWithSelection = urlGame.IsNullOrEmpty();
            }
            else
            {
                GetWithSelection = (urlGame.IsNullOrEmpty() || !PluginSettings.SelectOnlyWithoutStoreUrl);
            }

            if (GetWithSelection)
            {
                Common.LogDebug(true, $"Search url for {game.Name}");

                // Search game
                IndiegalaLibrarySearch ViewExtension = null;
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    ViewExtension = new IndiegalaLibrarySearch(Plugin.PlayniteApi, game.Name);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCMetaLookupWindowTitle"), ViewExtension);
                    windowExtension.ShowDialog();
                }));
                
                if (!ViewExtension.DataResponse.Name.IsNullOrEmpty())
                {
                    urlGame = ViewExtension.DataResponse.StoreUrl;
                    gameMetadata.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else
                {
                    Common.LogDebug(true, $"No url for {game.Name}");
                    return gameMetadata;
                }
            }
            
            if (urlGame.IsNullOrEmpty())
            {
                Common.LogDebug(true, $"No url for {game.Name}");
                return gameMetadata;
            }

            Common.LogDebug(true, $"urlGame: {urlGame}");

            string ResultWeb = string.Empty;
            using (var WebView = PlayniteApi.WebViews.CreateOffscreenView())
            {
                WebView.NavigateAndWait(urlGame);
                ResultWeb = WebView.GetPageSource();
            }

            if (!ResultWeb.IsNullOrEmpty())
            {
                if (ResultWeb.ToLower().Contains("request unsuccessful"))
                {
                    logger.Error($"Request unsuccessful for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"Request unsuccessful for {urlGame}", "IndiegalaLibrary");

                    return gameMetadata;
                }
                if (ResultWeb.ToLower().Contains("<body></body>"))
                {
                    logger.Error($"Request with no data for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"Request with no data for {urlGame}", "IndiegalaLibrary");

                    return gameMetadata;
                }

                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                if (htmlDocument.QuerySelector("h1.developer-product-title") != null)
                {
                    gameMetadata = ParseType1(htmlDocument, gameMetadata);
                    gameMetadata.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else if (htmlDocument.QuerySelector("h1.store-product-page-title") != null)
                {
                    gameMetadata = ParseType2(htmlDocument, gameMetadata);
                    gameMetadata.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else
                {
                    logger.Error($"No parser for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"No parser for {urlGame}", "IndiegalaLibrary");
                }
            }

            Common.LogDebug(true, $"metadata: {Serialization.ToJson(gameMetadata)}");
            return gameMetadata;
        }


        private GameMetadata ParseType1(IHtmlDocument htmlDocument, GameMetadata gameMetadata)
        {
            // Cover Image
            try
            {
                string CoverImage = htmlDocument.QuerySelector("figure.developer-product-cover img")?.GetAttribute("src");
                if (CoverImage.IsNullOrEmpty())
                {
                    CoverImage = htmlDocument.QuerySelector("figure.developer-product-cover img")?.GetAttribute("data-img-src");
                }
                if (!CoverImage.IsNullOrEmpty())
                {
                    gameMetadata.CoverImage = ResizeCoverImage(new MetadataFile(CoverImage));
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
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.developer-product-media-col img"))
                {
                    string imgSrc = SearchElement.GetAttribute("src");
                    if (imgSrc.IsNullOrEmpty())
                    {
                        imgSrc = SearchElement.GetAttribute("data-img-src");
                    }

                    if (imgSrc.IndexOf("indiegala") > -1)
                    {
                        possibleBackgrounds.Add(imgSrc);
                    }
                }
                if (possibleBackgrounds.Count > 0)
                {
                    // Selection mode
                    var settings = Plugin.LoadPluginSettings<IndiegalaLibrarySettings>();
                    Common.LogDebug(true, $"ImageSelectionPriority: {settings.ImageSelectionPriority}");

                    if (settings.ImageSelectionPriority == 0)
                    {
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        var selection = GetBackgroundManually(possibleBackgrounds);
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
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.developer-product-description"))
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
            foreach (var el in htmlDocument.QuerySelectorAll("div.developer-product-contacts li"))
            {
                switch (el.QuerySelector("i").GetAttribute("class").ToLower())
                {
                    case "fa fa-globe":
                        gameMetadata.Links.Add(new Link { Name = resources.GetString("LOCWebsiteLabel"), Url = el.QuerySelector("a").GetAttribute("href") });
                        break;
                    case "fa fa-facebook-official":
                        gameMetadata.Links.Add(new Link { Name = "Facebook", Url = el.QuerySelector("a").GetAttribute("href") });
                        break;
                    case "fa fa-twitter":
                        gameMetadata.Links.Add(new Link { Name = "Twitter", Url = el.QuerySelector("a").GetAttribute("href") });
                        break;
                }
            }

            // More
            try
            {
                var developerProduct = htmlDocument.QuerySelectorAll("div.developer-product-contents-aside-inner").First();
                foreach (var SearchElement in developerProduct.QuerySelectorAll("div.developer-product-contents-aside-block"))
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
                            foreach (var Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                HashSet<MetadataProperty> Genres = gameMetadata.Genres;
                                foreach (var genre in PlayniteApi.Database.Genres)
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
                            foreach (var Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
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
                    }
                }

                gameMetadata.Developers = new HashSet<MetadataProperty>
                {
                    new MetadataNameProperty(htmlDocument.QuerySelector("h2.developer-product-subtitle a").InnerHtml.Trim())
                };
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GameDetails");
            }

            return gameMetadata;
        }

        private GameMetadata ParseType2(IHtmlDocument htmlDocument, GameMetadata gameMetadata)
        {
            // Cover Image
            try
            {
                string CoverImage = htmlDocument.QuerySelector("div.main-info-box-resp img.img-fit")?.GetAttribute("src");
                if (!CoverImage.IsNullOrEmpty())
                {
                    gameMetadata.CoverImage = ResizeCoverImage(new MetadataFile(CoverImage));
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
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.media-caption-small img"))
                {
                    if (SearchElement.GetAttribute("src").IndexOf("indiegala") > -1)
                    {
                        possibleBackgrounds.Add(SearchElement.GetAttribute("src"));
                    }
                }
                if (possibleBackgrounds.Count > 0)
                {
                    // Selection mode
                    var settings = Plugin.LoadPluginSettings<IndiegalaLibrarySettings>();
                    Common.LogDebug(true, $"ImageSelectionPriority: {settings.ImageSelectionPriority}");

                    if (settings.ImageSelectionPriority == 0)
                    {
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        gameMetadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        var selection = GetBackgroundManually(possibleBackgrounds);
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
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("section.store-product-sub-info-box-resp div.info-row"))
                {
                    switch (SearchElement.QuerySelector("div.info-title").InnerHtml.ToLower())
                    {
                        case "publisher":
                            string strPublisher = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont a").InnerHtml);
                            Common.LogDebug(true, $"strPublisher: {strPublisher}");

                            gameMetadata.Publishers = new HashSet<MetadataProperty> { new MetadataNameProperty(strPublisher) };
                            break;
                        case "developer":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont").InnerHtml);
                            Common.LogDebug(true, $"strDevelopers: {strDevelopers}");

                            gameMetadata.Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(strDevelopers) };
                            break;
                        case "released":
                            string strReleased = SearchElement.QuerySelector("div.info-cont").InnerHtml;
                            Common.LogDebug(true, $"strReleased: {strReleased}");

                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                            {
                                gameMetadata.ReleaseDate = new ReleaseDate(dateTime);
                            }
                            break;
                        case "categories":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span a"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml);
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                HashSet<MetadataProperty> Genres = gameMetadata.Genres;
                                foreach (var genre in PlayniteApi.Database.Genres)
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
                            foreach (var Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span"))
                            {
                                string strModes = Element.InnerHtml;
                                Common.LogDebug(true, $"strModes: {strModes}");

                                HashSet<MetadataProperty> Features = gameMetadata.Genres;
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
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GameDetails");
            }

            return gameMetadata;
        }


        private MetadataFile ResizeCoverImage(MetadataFile OriginalMetadataFile)
        {
            MetadataFile metadataFile = OriginalMetadataFile;

            try
            {
                Stream imageStream = Web.DownloadFileStream(OriginalMetadataFile.Path).GetAwaiter().GetResult();
                ImageProperty imageProperty = ImageTools.GetImapeProperty(imageStream);

                string FileName = Path.GetFileNameWithoutExtension(OriginalMetadataFile.FileName);

                if (imageProperty != null)
                {
                    string NewCoverPath = Path.Combine(PlaynitePaths.ImagesCachePath, FileName);

                    if (imageProperty.Width <= imageProperty.Height)
                    {
                        int NewWidth = (int)(imageProperty.Width * MaxHeight / imageProperty.Height);
                        Common.LogDebug(true, $"FileName: {FileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewWidth: {NewWidth}");

                        ImageTools.Resize(imageStream, NewWidth, MaxHeight, NewCoverPath);
                    }
                    else
                    {
                        int NewHeight = (int)(imageProperty.Height * MaxWidth / imageProperty.Width);
                        Common.LogDebug(true, $"FileName: {FileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewHeight: {NewHeight}");

                        ImageTools.Resize(imageStream, MaxWidth, NewHeight, NewCoverPath);
                    }

                    Common.LogDebug(true, $"NewCoverPath: {NewCoverPath}.png");

                    if (File.Exists(NewCoverPath + ".png"))
                    {
                        Common.LogDebug(true, $"Used new image size");
                        metadataFile = new MetadataFile(FileName, File.ReadAllBytes(NewCoverPath + ".png"));
                    }
                    else
                    {
                        Common.LogDebug(true, $"Used OriginalUrl");
                        metadataFile = new MetadataFile(FileName, File.ReadAllBytes(NewCoverPath + ".png"));
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on resize CoverImage from {OriginalMetadataFile.Path}");
            }

            return metadataFile;
        }
    }
}
