using IndiegalaLibrary.Services;
using Playnite.SDK;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace IndiegalaLibrary.Views
{
    public partial class IndiegalaLibrarySettingsView : UserControl
    {
        private IPlayniteAPI PlayniteApi;
        private ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();
        
        private IndiegalaAccountClient IndiegalaApi;
        private IndiegalaLibrarySettings settings;


        public IndiegalaLibrarySettingsView(IPlayniteAPI PlayniteApi, IndiegalaLibrarySettings settings)
        {
            this.PlayniteApi = PlayniteApi;
            this.settings = settings;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            view = PlayniteApi.WebViews.CreateView(490, 670);
            IndiegalaApi = new IndiegalaAccountClient(view);

            InitializeComponent();

            CheckIsAuth();

            DataContext = this;
        }

        private void CheckIsAuth()
        {
            lIsAuth.Content = resources.GetString("LOCLoginChecking");
            var task = Task.Run(() => CheckLogged(IndiegalaApi))
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        if (antecedent.Result)
                        {
                            lIsAuth.Content = resources.GetString("LOCLoggedIn");
                        }
                        else
                        {
                            if (IndiegalaApi.GetIsUserLocked())
                            {
                                lIsAuth.Content = resources.GetString("LOCIndiegalaLockedError");
                            }
                            else
                            {
                                lIsAuth.Content = resources.GetString("LOCNotLoggedIn");
                            }
                        }
                    }));
                });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            lIsAuth.Content = "no authenticated";
            try
            {
                IWebView view = PlayniteApi.WebViews.CreateView(490, 670);
                IndiegalaApi = new IndiegalaAccountClient(view);
                IndiegalaApi.Login();

                if (IndiegalaApi.isConnected)
                {
                    lIsAuth.Content = resources.GetString("LOCLoggedIn");
                }
                else
                {
                    //lIsAuth.Content = resources.GetString("LOCNotLoggedIn");
                    Thread.Sleep(2000);
                    CheckIsAuth();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to authenticate user.");
            }
        }


        private bool CheckLogged(IndiegalaAccountClient IndiegalaApi)
        {
            return IndiegalaApi.GetIsUserLoggedIn();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            settings.ImageSelectionPriority = cbImageMode.SelectedIndex;
        }
    }
}
