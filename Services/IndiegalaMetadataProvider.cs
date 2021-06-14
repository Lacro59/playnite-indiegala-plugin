using AngleSharp.Dom.Html;
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

        private readonly IPlayniteAPI PlayniteApi;
        private readonly IndiegalaLibrary Plugin;

        private readonly int MaxHeight = 400;
        private readonly int MaxWidth = 400;


        public IndiegalaMetadataProvider(IndiegalaLibrary Plugin, IPlayniteAPI PlayniteApi)
        {
            this.PlayniteApi = PlayniteApi;
            this.Plugin = Plugin;
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
            var PluginSettings = Plugin.LoadPluginSettings<IndiegalaLibrarySettings>();

            if (PluginSettings.UseClient)
            {
                IndiegalaAccountClient indiegalaAccountClient = new IndiegalaAccountClient(null);

                var MetadataClient = indiegalaAccountClient.GetMetadataWithClient(game);
                if (MetadataClient != null)
                {
                    return MetadataClient;
                }
            }




            var gameInfo = new GameInfo() {
                Links = new List<Link>(),
                Tags = new List<string>(),
                Genres = new List<string>(),
                Features = new List<string>(),
                GameActions = new List<GameAction>()
            };

            var metadata = new GameMetadata()
            {
                GameInfo = gameInfo
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
                    ViewExtension = new IndiegalaLibrarySearch(game.Name);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCMetaLookupWindowTitle"), ViewExtension);
                    windowExtension.ShowDialog();
                }));
                
                if (!ViewExtension.DataResponse.Name.IsNullOrEmpty())
                {
                    urlGame = ViewExtension.DataResponse.StoreUrl;
                    gameInfo.Links.Add(new Link { Name = "Store", Url = urlGame });
                }
                else
                {
                    Common.LogDebug(true, $"No url for {game.Name}");
                    return metadata;
                }
            }
            
            if (urlGame.IsNullOrEmpty())
            {
                Common.LogDebug(true, $"No url for {game.Name}");
                return metadata;
            }

            Common.LogDebug(true, $"urlGame: {urlGame}");

            string ResultWeb = Web.DownloadStringData(urlGame).GetAwaiter().GetResult();

            if (!ResultWeb.IsNullOrEmpty())
            {
                if (ResultWeb.ToLower().Contains("request unsuccessful"))
                {
                    logger.Error($"GetMetadata() - Request unsuccessful for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"Request unsuccessful for {urlGame}", "IndiegalaLibrary");

                    return metadata;
                }
                if (ResultWeb.ToLower().Contains("<body></body>"))
                {
                    logger.Error($"GetMetadata() - Request with no data for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"Request with no data for {urlGame}", "IndiegalaLibrary");

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
                    logger.Error($"GetMetadata() - No parser for {urlGame}");
                    PlayniteApi.Dialogs.ShowErrorMessage($"No parser for {urlGame}", "IndiegalaLibrary");
                }
            }

            Common.LogDebug(true, $"metadata: {JsonConvert.SerializeObject(metadata)}");
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
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
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
                        metadata.GameInfo.Description = Description;
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
                            Common.LogDebug(true, $"strReleased: {strReleased}");

                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                            {
                                metadata.GameInfo.ReleaseDate = dateTime;
                            }
                            break;
                        case "categories":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.developer-product-contents-aside-text li"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml.Replace("<i aria-hidden=\"true\" class=\"fa fa-circle tcf-side-section-lb tcf-side-section-lbc\"></i>", string.Empty));
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                foreach (var genre in PlayniteApi.Database.Genres)
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
                                Common.LogDebug(true, $"strModes: {strModes}");

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
                Common.LogError(ex, false, $"Error on GameDetails");
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
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[0]);
                    }
                    else if (settings.ImageSelectionPriority == 1 || (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        metadata.BackgroundImage = new MetadataFile(possibleBackgrounds[index]);
                    }
                    else if (settings.ImageSelectionPriority == 2 && PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
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
                Common.LogError(ex, false, $"Error on BackgroundImage");
            }

            //Description 
            try
            {
                string Description = htmlDocument.QuerySelector("section.description-cont div.description div.description-inner").InnerHtml;
                metadata.GameInfo.Description = Description.Trim();
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

                            metadata.GameInfo.Publishers = new List<string> { strPublisher };
                            break;
                        case "developer":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont").InnerHtml);
                            Common.LogDebug(true, $"strDevelopers: {strDevelopers}");

                            metadata.GameInfo.Developers = new List<string> { strDevelopers };
                            break;
                        case "released":
                            string strReleased = SearchElement.QuerySelector("div.info-cont").InnerHtml;
                            Common.LogDebug(true, $"strReleased: {strReleased}");

                            if (DateTime.TryParseExact(strReleased, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                            {
                                metadata.GameInfo.ReleaseDate = dateTime;
                            }
                            break;
                        case "categories":
                            foreach (var Element in SearchElement.QuerySelectorAll("div.info-cont-hidden-inner span a"))
                            {
                                string strCategories = WebUtility.HtmlDecode(Element.InnerHtml);
                                Common.LogDebug(true, $"strCategories: {strCategories}");

                                foreach (var genre in PlayniteApi.Database.Genres)
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
                                Common.LogDebug(true, $"strModes: {strModes}");

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
                Common.LogError(ex, false, $"Error on GameDetails");
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
                Common.LogError(ex, false, $"Error on resize CoverImage from {OriginalMetadataFile.OriginalUrl}");
            }

            return metadataFile;
        }
    }
}
