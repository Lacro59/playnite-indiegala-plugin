using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using IndiegalaLibrary.Views;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Metadata;
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

        private readonly IPlayniteAPI api;
        private readonly IndiegalaLibrary library;

        private readonly int MaxHeight = 400;
        private readonly int MaxWidth = 400;


        public IndiegalaMetadataProvider(IndiegalaLibrary library, IPlayniteAPI api)
        {
            this.api = api;
            this.library = library;
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
                return api.Dialogs.ChooseImageFile(selection, resources.GetString("LOCSelectBackgroundTitle"));
            }
            else
            {
                return new ImageFileOption("nopath");
            }
        }


        public override GameMetadata GetMetadata(Game game)
        {
            var settings = library.LoadPluginSettings<IndiegalaLibrarySettings>();

            var gameInfo = new GameInfo() {
                Links = new List<Link>(),
                Tags = new List<string>(),
                Genres = new List<string>(),
                Features = new List<string>(),
                OtherActions = new List<GameAction>()
            };
            var metadata = new GameMetadata()
            {
                GameInfo = gameInfo
            };


            string urlGame = string.Empty;
            List<Link> Links = new List<Link>();
            foreach (var Link in game.Links)
            {
                if (Link.Name == "Store" && Link.Url.Contains("indiegala"))
                {
                    urlGame = Link.Url;

                    if (game.Links.Count == 1)
                    {
                        game.Links = null;
                    }
                }
                Links.Add(Link);
            }

            bool GetWithSelection = false;
            if (IndiegalaLibrary.IsLibrary)
            {
                GetWithSelection = urlGame.IsNullOrEmpty();
            }
            else
            {
                GetWithSelection = (urlGame.IsNullOrEmpty() || !settings.SelectOnlyWithoutStoreUrl);
            }

            if (GetWithSelection)
            {
#if DEBUG
                logger.Debug($"Indiegala [Ignored] - Search url for {game.Name}");
#endif

                // Search game
                IndiegalaLibrarySearch ViewExtension = null;
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    ViewExtension = new IndiegalaLibrarySearch(game.Name);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(api, resources.GetString("LOCMetaLookupWindowTitle"), ViewExtension);
                    windowExtension.ShowDialog();
                }));
                
                if (!ViewExtension.resultResponse.Name.IsNullOrEmpty())
                {
                    urlGame = ViewExtension.resultResponse.StoreUrl;
                    gameInfo.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else
                {
#if DEBUG
                    logger.Debug($"Indiegala [Ignored] - No url for {game.Name}");
#endif
                    return metadata;
                }
            }
            
            if (urlGame.IsNullOrEmpty())
            {
#if DEBUG
                logger.Debug($"Indiegala [Ignored] - No url for {game.Name}");
#endif
                return metadata;
            }

#if DEBUG
            logger.Debug($"Indiegala [Ignored] - urlGame: {urlGame}");
#endif

            string ResultWeb = Web.DownloadStringData(urlGame).GetAwaiter().GetResult();

            if (!ResultWeb.IsNullOrEmpty())
            {
#if DEBUG
                ResultWeb = ResultWeb.Replace(Environment.NewLine, string.Empty).Replace("\r\n", string.Empty);
                logger.Debug($"Indiegala [Ignored] - ResultWeb: {ResultWeb}");
#endif

                if (ResultWeb.ToLower().Contains("request unsuccessful"))
                {
                    logger.Error($"Indiegala - GetMetadata() - Request unsuccessful for {urlGame}");
                    api.Dialogs.ShowErrorMessage($"Request unsuccessful for {urlGame}", "IndiegalaLibrary");

                    return metadata;
                }
                if (ResultWeb.ToLower().Contains("<body></body>"))
                {
                    logger.Error($"Indiegala - GetMetadata() - Request with no data for {urlGame}");
                    api.Dialogs.ShowErrorMessage($"Request with no data for {urlGame}", "IndiegalaLibrary");

                    return metadata;
                }

                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                if (htmlDocument.QuerySelector("figure.developer-product-cover img") != null)
                {
                    metadata = ParseType1(htmlDocument, metadata);
                    metadata.GameInfo.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else if (htmlDocument.QuerySelector("div.media-caption-small img") != null)
                {
                    metadata = ParseType2(htmlDocument, metadata);
                    metadata.GameInfo.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else
                {
                    logger.Error($"Indiegala - GetMetadata() - No parser for {urlGame}");
                    api.Dialogs.ShowErrorMessage($"No parser for {urlGame}", "IndiegalaLibrary");
                }
            }

#if DEBUG
            logger.Debug($"Indiegala [Ignored] - metadata: {JsonConvert.SerializeObject(metadata)}");
#endif
            return metadata;
        }


        private GameMetadata ParseType1(IHtmlDocument htmlDocument, GameMetadata metadata)
        {
            // Cover Image
            try
            {
                string CoverImage = htmlDocument.QuerySelector("figure.developer-product-cover img").GetAttribute("src");
                if (CoverImage.IsNullOrEmpty())
                {
                    CoverImage = htmlDocument.QuerySelector("figure.developer-product-cover img").GetAttribute("data-img-src");
                }
                metadata.CoverImage = ResizeCoverImage(new MetadataFile(CoverImage));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on CoverImage");
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
                    var settings = library.LoadPluginSettings<IndiegalaLibrarySettings>();
#if DEBUG
                    logger.Debug($"Indiegala [Ignored] - ImageSelectionPriority: {settings.ImageSelectionPriority}");
#endif

                    if (settings.ImageSelectionPriority == 0)
                    {
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && api.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && api.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        var selection = GetBackgroundManually(possibleBackgrounds);
                        if (selection != null && selection.Path != "nopath")
                        {
                            metadata.BackgroundImage = new MetadataFile(selection.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on BackgroundImage");
            }


            //Description 
            try
            {
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.developer-product-description"))
                {
                    if (!SearchElement.GetAttribute("class").Contains("display"))
                    {
                        string Description = SearchElement.InnerHtml.Trim();
                        metadata.GameInfo.Description = Description;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on Description");
            }

            // Link 
            foreach (var el in htmlDocument.QuerySelectorAll("div.developer-product-contacts li"))
            {
                switch (el.QuerySelector("i").GetAttribute("class").ToLower())
                {
                    case "fa fa-globe":
                        metadata.GameInfo.Links.Add(new Link { Name = resources.GetString("LOCWebsiteLabel"), Url = el.QuerySelector("a").GetAttribute("href") });
                        break;
                    case "fa fa-facebook-official":
                        metadata.GameInfo.Links.Add(new Link { Name = "Facebook", Url = el.QuerySelector("a").GetAttribute("href") });
                        break;
                    case "fa fa-twitter":
                        metadata.GameInfo.Links.Add(new Link { Name = "Twitter", Url = el.QuerySelector("a").GetAttribute("href") });
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
#if DEBUG
                            logger.Debug($"Indiegala [Ignored] - strReleased: {strReleased}");
#endif
                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                            {
                                metadata.GameInfo.ReleaseDate = dateTime;
                            }
                            break;
                        case "categories":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
#if DEBUG
                                logger.Debug($"Indiegala [Ignored] - strCategories: {strCategories}");
#endif
                                foreach (var genre in api.Database.Genres)
                                {
                                    if (genre.Name.ToLower() == strCategories.ToLower())
                                    {
                                        metadata.GameInfo.Genres.Add(genre.Name);
                                    }
                                }
                            }
                            break;
                        case "specs":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strModes = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
#if DEBUG
                                logger.Debug($"Indiegala [Ignored] - strModes: {strModes}");
#endif
                                if (strModes.ToLower() == "single-player")
                                {
                                    metadata.GameInfo.Features.Add("Single Player");
                                }
                                if (strModes.ToLower() == "full controller support")
                                {
                                    metadata.GameInfo.Features.Add("Full Controller Support");
                                }
                            }
                            break;
                    }
                }

                metadata.GameInfo.Developers = new List<string>
                {
                    htmlDocument.QuerySelector("h2.developer-product-subtitle a").InnerHtml.Trim()
                };
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on GameDetails");
            }

            return metadata;
        }

        private GameMetadata ParseType2(IHtmlDocument htmlDocument, GameMetadata metadata)
        {
            // Cover Image
            string CoverImage = string.Empty;
            try
            {
                var HtmCover = htmlDocument.QuerySelector("div.main-info-box-resp img.img-fit");
                if (HtmCover != null)
                {
                    CoverImage = HtmCover.GetAttribute("src");

                    if (!CoverImage.IsNullOrEmpty())
                    {
                        metadata.CoverImage = ResizeCoverImage(new MetadataFile(CoverImage));
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on CoverImage");
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
                    var settings = library.LoadPluginSettings<IndiegalaLibrarySettings>();
#if DEBUG
                    logger.Debug($"Indiegala [Ignored] - ImageSelectionPriority: {settings.ImageSelectionPriority}");
#endif

                    if (settings.ImageSelectionPriority == 0)
                    {
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && api.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && api.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        var selection = GetBackgroundManually(possibleBackgrounds);
                        if (selection != null && selection.Path != "nopath")
                        {
                            metadata.BackgroundImage = new MetadataFile(selection.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on BackgroundImage");
            }

            //Description 
            try
            {
                string Description = htmlDocument.QuerySelector("section.description-cont div.description div.description-inner").InnerHtml;
                metadata.GameInfo.Description = Description.Trim();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on Description");
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
#if DEBUG
                            logger.Debug($"Indiegala [Ignored] - strPublisher: {strPublisher}");
#endif
                            metadata.GameInfo.Publishers = new List<string> { strPublisher };
                            break;
                        case "developer":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont").InnerHtml);
#if DEBUG
                            logger.Debug($"Indiegala [Ignored] - strDevelopers: {strDevelopers}");
#endif
                            metadata.GameInfo.Developers = new List<string> { strDevelopers };
                            break;
                        case "released":
                            string strReleased = SearchElement.QuerySelector("div.info-cont").InnerHtml;
#if DEBUG
                            logger.Debug($"Indiegala [Ignored] - strReleased: {strReleased}");
#endif
                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                            {
                                metadata.GameInfo.ReleaseDate = dateTime;
                            }
                            break;
                        case "categories":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span a"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml);
#if DEBUG
                                logger.Debug($"Indiegala [Ignored] - strCategories: {strCategories}");
#endif
                                foreach (var genre in api.Database.Genres)
                                {
                                    if (genre.Name.ToLower() == strCategories.ToLower())
                                    {
                                        metadata.GameInfo.Genres.Add(genre.Name);
                                    }
                                }
                            }
                            break;
                        case "modes":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span"))
                            {
                                string strModes = Element.InnerHtml;
#if DEBUG
                                logger.Debug($"Indiegala [Ignored] - strModes: {strModes}");
#endif
                                if (strModes.ToLower() == "single-player")
                                {
                                    metadata.GameInfo.Features.Add("Single Player");
                                }
                                if (strModes.ToLower() == "full controller support")
                                {
                                    metadata.GameInfo.Features.Add("Full Controller Support");
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on GameDetails");
            }

            return metadata;
        }


        private MetadataFile ResizeCoverImage(MetadataFile OriginalMetadataFile)
        {
            MetadataFile metadataFile = OriginalMetadataFile;

            try
            {
                Stream imageStream = Web.DownloadFileStream(OriginalMetadataFile.OriginalUrl).GetAwaiter().GetResult();
                ImageProperty imageProperty = ImageTools.GetImapeProperty(imageStream);

                string FileName = Path.GetFileNameWithoutExtension(OriginalMetadataFile.FileName);

                if (imageProperty != null)
                {
                    string NewCoverPath = Path.Combine(PlaynitePaths.ImagesCachePath, FileName);

                    if (imageProperty.Width <= imageProperty.Height)
                    {
                        int NewWidth = (int)(imageProperty.Width * MaxHeight / imageProperty.Height);
#if DEBUG
                        logger.Debug($"IndiegalaLibrary [Ignored] - FileName: {FileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewWidth: {NewWidth}");
#endif
                        ImageTools.Resize(imageStream, NewWidth, MaxHeight, NewCoverPath);
                    }
                    else
                    {
                        int NewHeight = (int)(imageProperty.Height * MaxWidth / imageProperty.Width);
#if DEBUG
                        logger.Debug($"IndiegalaLibrary [Ignored] - FileName: {FileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewHeight: {NewHeight}");
#endif
                        ImageTools.Resize(imageStream, MaxWidth, NewHeight, NewCoverPath);
                    }

#if DEBUG
                    logger.Debug($"IndiegalaLibrary [Ignored] - NewCoverPath: {NewCoverPath}.png");
#endif
                    if (File.Exists(NewCoverPath + ".png"))
                    {
#if DEBUG
                        logger.Debug($"IndiegalaLibrary [Ignored] - Used new image size");
#endif
                        metadataFile = new MetadataFile(FileName, File.ReadAllBytes(NewCoverPath + ".png"));
                    }
                    else
                    {
#if DEBUG
                        logger.Debug($"IndiegalaLibrary [Ignored] - Used OriginalUrl");
#endif
                        metadataFile = new MetadataFile(FileName, File.ReadAllBytes(NewCoverPath + ".png"));
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "IndiegalaLibrary", $"Error on resize CoverImage from {OriginalMetadataFile.OriginalUrl}");
            }

            return metadataFile;
        }
    }
}
