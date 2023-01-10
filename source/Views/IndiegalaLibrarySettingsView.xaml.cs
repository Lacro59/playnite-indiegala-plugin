using CommonPluginsShared;
using IndiegalaLibrary.Services;
using Playnite.SDK;
using System;
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
        private IndiegalaLibrarySettings Settings;


        public IndiegalaLibrarySettingsView(IPlayniteAPI PlayniteApi, IndiegalaLibrarySettings Settings)
        {
            this.PlayniteApi = PlayniteApi;
            this.Settings = Settings;

            var view = PlayniteApi.WebViews.CreateOffscreenView();
            IndiegalaApi = new IndiegalaAccountClient();

            InitializeComponent();
            DataContext = this;


            IndieglaClient indieglaClient = new IndieglaClient();

            CheckIsAuthWithoutClient();

            if (!indieglaClient.IsInstalled)
            {
                PART_UseClient.IsChecked = false;
                PART_UseClient.IsEnabled = false;
            }
        }


        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.ImageSelectionPriority = cbImageMode.SelectedIndex;
        }



        #region With client
        /*
        private void PART_UseClient_Checked(object sender, RoutedEventArgs e)
        {
            PART_LabelAuthWithoutClient.Content = string.Empty;
            CheckIsAuthWithClient();
        }


        private void CheckIsAuthWithClient()
        {
            IndiegalaApi.ResetClientCookies();
            PART_LabelAuthWithClient.Content = resources.GetString("LOCLoginChecking");

            var task = Task.Run(() => IndiegalaApi.GetIsUserLoggedInWithClient())
                .ContinueWith(antecedent =>
                {
                    try
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            if (antecedent.Result)
                            {
                                PART_LabelAuthWithClient.Content = resources.GetString("LOCCommonLoggedIn");
                            }
                            else
                            {
                                if (IndiegalaApi.GetIsUserLocked())
                                {
                                    PART_LabelAuthWithClient.Content = resources.GetString("LOCIndiegalaLockedError");
                                }
                                else
                                {
                                    PART_LabelAuthWithClient.Content = resources.GetString("LOCCommonNotLoggedIn");
                                }
                            }
                        }));
                    }
                    catch { }
                });
        }

        private void Button_ClickWithClient(object sender, RoutedEventArgs e)
        {
            IndiegalaApi.ResetClientCookies();
            PART_LabelAuthWithClient.Content = resources.GetString("LOCCommonLoginChecking");

            try
            {
                Task.Run(() =>
                {
                    IndiegalaApi.LoginWithClient();

                    var cts = new CancellationTokenSource();
                    var token = cts.Token;
                    try
                    {
                        cts.CancelAfter(60000);
                        Task.Run(() =>
                        {
                            while (!IndiegalaApi.isConnected)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    return;
                                }

                                Thread.Sleep(1000);

                                this.Dispatcher.Invoke(() =>
                                {
                                    CheckIsAuthWithClient();
                                });
                            }
                        }, token);
                    }
                    catch (OperationCanceledException)
                    {
                        //handle cancellation
                    }
                    catch (Exception)
                    {
                        //handle exception
                    }
                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Failed to authenticate user with client");
            }
        }
        */
        #endregion


        #region Without client
        private void PART_UseClient_Unchecked(object sender, RoutedEventArgs e)
        {
            //PART_LabelAuthWithClient.Content = string.Empty;
            CheckIsAuthWithoutClient();
        }

        private void CheckIsAuthWithoutClient()
        {
            PART_Unlock.Visibility = Visibility.Collapsed;
            IndiegalaApi.ResetClientCookies();
            PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonLoginChecking");

            var task = Task.Run(() => IndiegalaApi.GetIsUserLoggedInWithoutClient())
                .ContinueWith(antecedent =>
                {
                    try
                    {
                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            switch (antecedent.Result)
                            {
                                case ConnectionState.Locked:
                                    PART_Unlock.Visibility = Visibility.Visible;
                                    PART_LabelAuthWithoutClient.Content = resources.GetString("LOCIndiegalaLockedError");
                                    break;

                                case ConnectionState.Unlogged:
                                    PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonNotLoggedIn");
                                    break;

                                case ConnectionState.Logged:
                                    PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonLoggedIn");
                                    break;
                            }
                        }));
                    }
                    catch { }
                });
        }

        private void Button_ClickWithoutClient(object sender, RoutedEventArgs e)
        {
            IndiegalaApi.ResetClientCookies();
            PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonLoginChecking");

            try
            {
                IndiegalaApi.LoginWithoutClient();

                if (IndiegalaApi.isConnected)
                {
                    PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonLoggedIn");
                }
                else
                {
                    PART_LabelAuthWithoutClient.Content = resources.GetString("LOCCommonNotLoggedIn");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Failed to authenticate user without client");
            }
        }

        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFolder = PlayniteApi.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                PART_InstallPath.Text = SelectedFolder;
                Settings.InstallPath = SelectedFolder;
            }
        }
        #endregion


        private void PART_Unlock_Click(object sender, RoutedEventArgs e)
        {
            IndiegalaLibrary.OpenProfilForUnlocked();
            CheckIsAuthWithoutClient();
        }
    }
}
