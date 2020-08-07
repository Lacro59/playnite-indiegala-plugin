using IndiegalaLibrary.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IndiegalaLibrary.Views
{
    public partial class IndiegalaLibrarySettingsView : UserControl
    {
        private IPlayniteAPI PlayniteApi;
        private ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();
        
        private IndiegalaAccountClient IndiegalaApi;


        public IndiegalaLibrarySettingsView(IPlayniteAPI PlayniteApi)
        {
            this.PlayniteApi = PlayniteApi;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            IndiegalaApi = new IndiegalaAccountClient(view);

            InitializeComponent();


            lIsAuth.Content = resources.GetString("LOCLoginChecking");
            var task = Task.Run(() => CheckLogged(IndiegalaApi))
                .ContinueWith(antecedent =>
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => {
                        lIsAuth.Content = resources.GetString("LOCNotLoggedIn");
                        if (antecedent.Result)
                        {
                            lIsAuth.Content = resources.GetString("LOCLoggedIn");
                        }
                    }));
                });

            DataContext = this;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            lIsAuth.Content = "no authenticated";
            try
            {
                var view = PlayniteApi.WebViews.CreateView(490, 670);
                IndiegalaApi = new IndiegalaAccountClient(view);
                IndiegalaApi.Login();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to authenticate user.");
            }
        }


        private async Task<bool> CheckLogged(IndiegalaAccountClient IndiegalaApi)
        {
            return IndiegalaApi.GetIsUserLoggedIn();
        }
    }
}