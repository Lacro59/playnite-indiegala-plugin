using CommonPluginsShared;
using IndiegalaLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaApi
    {
        private static ILogger logger => LogManager.GetLogger();


        private static string apiUrl => "https://www.indiegala.com";
        private static string apiUserInfo => $"{apiUrl}/login_new/user_info";
        private static string apiGameDetails => @"https://developers.indiegala.com/get_product_info?dev_id={0}&prod_name={1}";


        private static List<UserCollection> userCollections { get; set; } = new List<UserCollection>();


        private static List<HttpCookie> _IgCookies = new List<HttpCookie>();
        private static List<HttpCookie> IgCookies
        {
            get
            {
                // TODO Add reset cookies
                if (_IgCookies?.Count <= 0)
                {
                    using (var WebViewOffscreen = API.Instance.WebViews.CreateOffscreenView())
                    {
                        List<HttpCookie> Cookies = WebViewOffscreen.GetCookies();
                        _IgCookies = Cookies.Where(x => x != null && x.Domain != null && x.Domain.Contains("indiegala", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    }
                }
                return _IgCookies;
            }
            set => _IgCookies = value;
        }


        public static List<UserCollection> GetUserCollections()
        {
            if (IndiegalaApi.userCollections?.Count > 0)
            {
                return IndiegalaApi.userCollections;
            }

            try
            {
                if (IgCookies.Count == 0)
                {
                    logger.Warn($"IndiegalaApi.GetUserCollections() - No cookies");
                    API.Instance.Notifications.Add(new NotificationMessage(
                        "Indiegala-Error-UserCollections",
                        "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCCommonLoginRequired"),
                        NotificationType.Error,
                        () =>
                        {
                            try
                            {
                                API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                            }
                            catch { }
                        }));
                    return new List<UserCollection>();
                }

                string response = Web.DownloadStringData(apiUserInfo, IgCookies, "galaClient").GetAwaiter().GetResult();
                if (!response.IsNullOrEmpty())
                {
                    dynamic data = Serialization.FromJson<dynamic>(response);
                    string userCollectionString = Serialization.ToJson(data["showcase_content"]["content"]["user_collection"]);
                    List<UserCollection> userCollections = Serialization.FromJson<List<UserCollection>>(userCollectionString);

                    IndiegalaApi.userCollections = userCollections;
                    return IndiegalaApi.userCollections;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
                API.Instance.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-UserCollections",
                    "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCCommonLoginRequired"),
                    NotificationType.Error,
                    () =>
                    {
                        try
                        {
                            API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                        }
                        catch { }
                    }));
            }

            return new List<UserCollection>();
        }


        public static ClientGameDetails GetClientGameDetails(UserCollection userCollection)
        {
            return IndiegalaApi.GetClientGameDetails(userCollection.prod_dev_namespace, userCollection.prod_slugged_name);
        }

        public static ClientGameDetails GetClientGameDetails(string prod_dev_namespace, string prod_slugged_name)
        {
            try
            {
                if (IgCookies.Count == 0)
                {
                    logger.Warn($"IndiegalaApi.GetUserCollections() - No cookies");
                    API.Instance.Notifications.Add(new NotificationMessage(
                        "Indiegala-Error-UserCollections",
                        "Indiegala" + System.Environment.NewLine + API.Instance.Resources.GetString("LOCCommonLoginRequired"),
                        NotificationType.Error,
                        () =>
                        {
                            try
                            {
                                API.Instance.Addons.Plugins.FirstOrDefault(p => p.Id == Guid.Parse("f7da6eb0-17d7-497c-92fd-347050914954")).OpenSettingsView();
                            }
                            catch { }
                        }));
                    return null;
                }

                string response = Web.DownloadStringData(string.Format(apiGameDetails, prod_dev_namespace, prod_slugged_name), IgCookies, "galaClient").GetAwaiter().GetResult();
                if (!response.IsNullOrEmpty() && !response.Contains("\"product_data\": 404"))
                {
                    ClientGameDetails data = Serialization.FromJson<ClientGameDetails>(response);
                    return data;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
                API.Instance.Notifications.Add(new NotificationMessage(
                    "Indiegala-Error-ClientGameDetailss",
                    "Indiegala",
                    NotificationType.Error));
            }

            return null;
        }



    }
}
