﻿using AngleSharp.Dom.Html;
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
using CommonPlayniteShared;
using CommonPlayniteShared.Common;
using AngleSharp.Dom;
using CommonPluginsShared.Extensions;
using IndiegalaLibrary.Models;
using IndiegalaLibrary.Models.GalaClient;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaMetadataProvider : LibraryMetadataProvider
    {
        private static ILogger Logger => LogManager.GetLogger();

        private IndiegalaLibrary Plugin { get; }
        private IndiegalaLibrarySettings Settings { get; }

        private int MaxHeight => 400;
        private int MaxWidth => 400;


        public IndiegalaMetadataProvider(IndiegalaLibrary plugin, IndiegalaLibrarySettings settings)
        {
            Plugin = plugin;
            Settings = settings;
        }


        private ImageFileOption GetBackgroundManually(List<string> possibleBackground)
        {
            List<ImageFileOption> selection = possibleBackground?.Select(x => new ImageFileOption { Path = x })?.ToList() ?? new List<ImageFileOption>();
            return selection.Count > 0
                ? API.Instance.Dialogs.ChooseImageFile(selection, ResourceProvider.GetString("LOCSelectBackgroundTitle"))
                : new ImageFileOption("nopath");
        }


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
                Common.LogDebug(true, $"Search url for {game.Name}");

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
                foreach (IElement SearchElement in htmlDocument.QuerySelectorAll("div.developer-product-media-col img"))
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


        private MetadataFile ResizeCoverImage(MetadataFile originalMetadataFile)
        {
            MetadataFile metadataFile = originalMetadataFile;

            try
            {
                Stream imageStream = Web.DownloadFileStream(originalMetadataFile.Path).GetAwaiter().GetResult();
                ImageProperty imageProperty = ImageTools.GetImapeProperty(imageStream);

                string FileName = Path.GetFileNameWithoutExtension(originalMetadataFile.FileName);

                if (imageProperty != null)
                {
                    string NewCoverPath = Path.Combine(PlaynitePaths.ImagesCachePath, FileName);

                    if (imageProperty.Width <= imageProperty.Height)
                    {
                        int NewWidth = imageProperty.Width * MaxHeight / imageProperty.Height;
                        Common.LogDebug(true, $"FileName: {FileName} - Width: {imageProperty.Width} - Height: {imageProperty.Height} - NewWidth: {NewWidth}");

                        ImageTools.Resize(imageStream, NewWidth, MaxHeight, NewCoverPath);
                    }
                    else
                    {
                        int NewHeight = imageProperty.Height * MaxWidth / imageProperty.Width;
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
                Common.LogError(ex, false, $"Error on resize CoverImage from {originalMetadataFile.Path}");
            }

            return metadataFile;
        }
    }
}
