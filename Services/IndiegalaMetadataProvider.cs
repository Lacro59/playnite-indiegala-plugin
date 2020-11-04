using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using IndiegalaLibrary.Views;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Metadata;
using Playnite.SDK.Models;
using PluginCommon;
using PluginCommon.PlayniteResources.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Windows;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaMetadataProvider : LibraryMetadataProvider
    {
        private ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private readonly IPlayniteAPI api;
        private readonly IndiegalaLibrary library;


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
                }
                Links.Add(Link);
            }

            if (urlGame.IsNullOrEmpty() || !settings.SelectOnlyWithoutStoreUrl)
            {
#if DEBUG
                logger.Debug($"Indiegala - Search url for {game.Name}");
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
                    logger.Debug($"Indiegala - No url for {game.Name}");
#endif
                    return metadata;
                }
            }

#if DEBUG
            logger.Debug($"Indiegala - urlGame: {urlGame}");
#endif

            string ResultWeb = Web.DownloadStringData(urlGame).GetAwaiter().GetResult();
            if (!ResultWeb.IsNullOrEmpty())
            {
                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

                if (htmlDocument.QuerySelector("div.dev-cover img.img-fit") != null)
                {
                    metadata = ParseType1(htmlDocument, metadata);
                }
                else
                {
                    metadata = ParseType2(htmlDocument, metadata);
                }
            }

#if DEBUG
            logger.Debug($"Indiegala - metadata: {JsonConvert.SerializeObject(metadata)}");
#endif
            return metadata;
        }

        private GameMetadata ParseType1(IHtmlDocument htmlDocument, GameMetadata metadata)
        {
            // Cover Image
            try
            {
                string CoverImage = htmlDocument.QuerySelector("div.dev-cover img.img-fit").GetAttribute("src");
                metadata.CoverImage = new MetadataFile(CoverImage);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on CoverImage");
            }

            //Background Image
            try
            {
                List<string> possibleBackgrounds = new List<string>();
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.dev-product-list div.dev-product-item-col a"))
                {
                    if (SearchElement.GetAttribute("href").IndexOf("indiegala") > -1)
                    {
                        possibleBackgrounds.Add(SearchElement.GetAttribute("href"));
                    }
                }
                if (possibleBackgrounds.Count > 0)
                {
                    // Selection mode
                    var settings = library.LoadPluginSettings<IndiegalaLibrarySettings>();

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
                string Description = htmlDocument.QuerySelector("div.dev-other-text").InnerHtml;
                Description = Description.Replace("<h3><strong><a href=\"https://www.indiegala.com/showcase\">Check out ALL the ongoing FREEbies here.</a></strong></h3>", string.Empty);
                metadata.GameInfo.Description = Description.Trim();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on Description");
            }

            // TODO MetadaProvider KO - Only work with LibraryProvider
            string UrlDownload = string.Empty;
            IEnumerable<IComment> comments = htmlDocument.Descendents<IComment>();
            foreach (IComment comment in comments)
            {
                var textValue = comment.TextContent;
                string findStartText = "<a class=\"custom-link-color\" href=\"";
                string findEndText = "\" target=";
                if (textValue.IndexOf(findStartText) > -1)
                {
                    int start = textValue.IndexOf(findStartText) + findStartText.Length;
                    UrlDownload = textValue.Substring(start);
                    int end = UrlDownload.IndexOf(findEndText);
                    UrlDownload = UrlDownload.Substring(0, end);
                    break;
                }
            }
            if (!UrlDownload.IsNullOrEmpty())
            {
#if DEBUG
                logger.Debug($"IndieGalaLibrary - UrlDownload: {UrlDownload}");
#endif
                var DownloadAction = new GameAction()
                {
                    Name = "Download",
                    Type = GameActionType.URL,
                    Path = UrlDownload,
                    IsHandledByPlugin = true
                };
                metadata.GameInfo.OtherActions = new List<GameAction> { DownloadAction };
            }

            // Link 
            foreach (var el in htmlDocument.QuerySelectorAll("div.dev-social-link"))
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

            try
            {
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("div.dev-info-data"))
                {
                    switch (SearchElement.QuerySelector("div.dev-info-data-key").InnerHtml.ToLower())
                    {
                        case "published":
                            string strDate = SearchElement.QuerySelector("div.dev-info-data-value").InnerHtml;
#if DEBUG
                            logger.Debug($"Indiegala - strDate: {strDate}");
#endif
                            if (DateTime.TryParseExact(strDate, "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                            {
                                metadata.GameInfo.ReleaseDate = dateTime;
                            }
                            break;
                        case "author":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.dev-info-data-value").InnerHtml);
#if DEBUG
                            logger.Debug($"Indiegala - strDevelopers: {strDevelopers}");
#endif
                            metadata.GameInfo.Developers = new List<string> { strDevelopers };
                            break;
                        case "specs":
                            string strFeatures = SearchElement.QuerySelector("div.dev-info-data-value").InnerHtml;
#if DEBUG
                            logger.Debug($"Indiegala - strFeatures: {strFeatures}");
#endif
                            if (strFeatures.ToLower() == "single-player")
                            {
                                metadata.GameInfo.Features.Add("Single Player");
                            }
                            if (strFeatures.ToLower() == "full controller support")
                            {
                                metadata.GameInfo.Features.Add("Full Controller Support");
                            }
                            break;
                        case "tags":
                            foreach (var TagElement in SearchElement.QuerySelectorAll("div.dev-info-data-value"))
                            {
                                string strTag = WebUtility.HtmlDecode(TagElement.InnerHtml);
#if DEBUG
                                logger.Debug($"Indiegala - strTag: {strTag}");
#endif
                                foreach (var genre in api.Database.Genres)
                                {
                                    if (genre.Name.ToLower() == strTag.ToLower())
                                    {
                                        metadata.GameInfo.Genres.Add(genre.Name);
                                    }
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

        private GameMetadata ParseType2(IHtmlDocument htmlDocument, GameMetadata metadata)
        {
            // Cover Image
            try
            {
                string CoverImage = htmlDocument.QuerySelector("div.main-info-box-resp img.img-fit").GetAttribute("src");
                metadata.CoverImage = new MetadataFile(CoverImage);
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
                Description = Description.Replace("<h3><strong><a href=\"https://www.indiegala.com/showcase\">Check out ALL the ongoing FREEbies here.</a></strong></h3>", string.Empty);
                metadata.GameInfo.Description = Description.Trim();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "Indiegala", $"Error on Description");
            }

            // TODO MetadaProvider KO - Only work with LibraryProvider
            /*
            string UrlDownload = string.Empty;
            IEnumerable<IComment> comments = htmlDocument.Descendents<IComment>();
            foreach (IComment comment in comments)
            {
                var textValue = comment.TextContent;
                string findStartText = "<a class=\"custom-link-color\" href=\"";
                string findEndText = "\" target=";
                if (textValue.IndexOf(findStartText) > -1)
                {
                    int start = textValue.IndexOf(findStartText) + findStartText.Length;
                    UrlDownload = textValue.Substring(start);
                    int end = UrlDownload.IndexOf(findEndText);
                    UrlDownload = UrlDownload.Substring(0, end);
                    break;
                }
            }
            if (!UrlDownload.IsNullOrEmpty())
            {
#if DEBUG
                logger.Debug($"IndieGalaLibrary - UrlDownload: {UrlDownload}");
#endif
                var DownloadAction = new GameAction()
                {
                    Name = "Download",
                    Type = GameActionType.URL,
                    Path = UrlDownload,
                    IsHandledByPlugin = true
                };
                metadata.GameInfo.OtherActions = new List<GameAction> { DownloadAction };
            }
            */

            // Link 
            /*
            foreach (var el in htmlDocument.QuerySelectorAll("div.dev-social-link"))
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
            */

            try
            {
                foreach (var SearchElement in htmlDocument.QuerySelectorAll("section.store-product-sub-info-box-resp div.info-row"))
                {
                    switch (SearchElement.QuerySelector("div.info-title").InnerHtml.ToLower())
                    {
                        case "publisher":
                            string strPublisher = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont a").InnerHtml);
#if DEBUG
                            logger.Debug($"Indiegala - strPublisher: {strPublisher}");
#endif
                            metadata.GameInfo.Publishers = new List<string> { strPublisher };
                            break;
                        case "developer":
                            string strDevelopers = WebUtility.HtmlDecode(SearchElement.QuerySelector("div.info-cont").InnerHtml);
#if DEBUG
                            logger.Debug($"Indiegala - strDevelopers: {strDevelopers}");
#endif
                            metadata.GameInfo.Developers = new List<string> { strDevelopers };
                            break;
                        case "released":
                            string strReleased = SearchElement.QuerySelector("div.info-cont").InnerHtml;
#if DEBUG
                            logger.Debug($"Indiegala - strReleased: {strReleased}");
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
                                logger.Debug($"Indiegala - strCategories: {strCategories}");
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
                                logger.Debug($"Indiegala - strModes: {strModes}");
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
    }
}
