using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Metadata;
using Playnite.SDK.Models;
using PluginCommon;
using PluginCommon.PlayniteResources.Common;
using System;
using System.Collections.Generic;
using System.Globalization;

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
            foreach(var Link in game.Links)
            {
                if (Link.Name == "Store")
                {
                    urlGame = Link.Url;
                }
            }


            if (urlGame.IsNullOrEmpty())
            {
                return metadata;
            }

#if DEBUG
            logger.Debug($"Indiegala - urlGame: {urlGame}");
#endif

            IWebView webView = api.WebViews.CreateOffscreenView();
            webView.NavigateAndWait(urlGame);
            string ResultWeb = webView.GetPageSource();
            if (!ResultWeb.IsNullOrEmpty())
            {
                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(ResultWeb);

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
                try { 
                    List<string> possibleBackgrounds = new List<string>();
                    foreach(var SearchElement in htmlDocument.QuerySelectorAll("div.dev-product-list div.dev-product-item-col a"))
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
                try { 
                    string Description = htmlDocument.QuerySelector("div.dev-other-text").InnerHtml;
                    Description = Description.Replace("<h3><strong><a href=\"https://www.indiegala.com/showcase\">Check out ALL the ongoing FREEbies here.</a></strong></h3>", string.Empty);
                    gameInfo.Description = Description.Trim();
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
                    gameInfo.OtherActions = new List<GameAction> { DownloadAction };
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
                                    gameInfo.ReleaseDate = dateTime;
                                }
                                break;
                            case "author":
                                string strDevelopers = SearchElement.QuerySelector("div.dev-info-data-value").InnerHtml;
#if DEBUG
                                logger.Debug($"Indiegala - strDevelopers: {strDevelopers}");
#endif
                                gameInfo.Developers = new List<string> { strDevelopers };
                                break;
                            case "specs":
                                string strFeatures = SearchElement.QuerySelector("div.dev-info-data-value").InnerHtml;
#if DEBUG
                                logger.Debug($"Indiegala - strFeatures: {strFeatures}");
#endif
                                if (strFeatures.ToLower() == "single-player")
                                {
                                    gameInfo.Features.Add("Single Player");
                                }
                                break;
                            case "tags":
                                foreach (var TagElement in SearchElement.QuerySelectorAll("div.dev-info-data-value"))
                                {
                                    string strTag = TagElement.InnerHtml;
#if DEBUG
                                    logger.Debug($"Indiegala - strTag: {strTag}");
#endif
                                    foreach (var genre in api.Database.Genres)
                                    {
                                        if (genre.Name.ToLower() == strTag.ToLower())
                                        {
                                            gameInfo.Genres.Add(genre.Name);
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
            }

#if DEBUG
            logger.Debug($"Indiegala - metadata: {JsonConvert.SerializeObject(metadata)}");
#endif
            return metadata;
        }
    }
}
