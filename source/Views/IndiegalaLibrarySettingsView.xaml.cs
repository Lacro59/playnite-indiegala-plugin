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
        private static ILogger Logger => LogManager.GetLogger();

        private static IndiegalaApi IndiegalaApi => IndiegalaLibrary.IndiegalaApi;
        private IndiegalaLibrarySettings Settings { get; }


        public IndiegalaLibrarySettingsView(IndiegalaLibrarySettings settings)
        {
            Settings = settings;

            InitializeComponent();
            DataContext = this;

            CheckIsAuthWithoutClient();

            /*
            if (!IndiegalaLibrary.IndiegalaClient.IsInstalled)
            {
                PART_UseClient.IsEnabled = false;
            }
            else
            {
                PART_Path.IsEnabled = false;
            }
            */
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
            CheckIsAuthWithoutClient();
        }

        private void CheckIsAuthWithoutClient()
        {
            PART_Unlock.Visibility = Visibility.Collapsed;
            PART_LabelAuthWithoutClient.Content = ResourceProvider.GetString("LOCCommonLoginChecking");

            _ = Task.Run(() =>
            {
                if (IndiegalaApi.IsUserLoggedIn)
                {
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        PART_LabelAuthWithoutClient.Content = ResourceProvider.GetString("LOCCommonLoggedIn");
                    }));
                }
                else
                {
                    Application.Current.Dispatcher?.Invoke(new Action(() =>
                    {
                        PART_LabelAuthWithoutClient.Content = ResourceProvider.GetString("LOCCommonNotLoggedIn");
                    }));
                }
            });
        }

        private void Button_ClickWithoutClient(object sender, RoutedEventArgs e)
        {
            PART_LabelAuthWithoutClient.Content = ResourceProvider.GetString("LOCCommonLoginChecking");

            try
            {
                IndiegalaApi.Login();
                PART_LabelAuthWithoutClient.Content = IndiegalaApi.IsUserLoggedIn
                    ? ResourceProvider.GetString("LOCCommonLoggedIn")
                    : ResourceProvider.GetString("LOCCommonNotLoggedIn");
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Failed to authenticate user without client");
            }
        }

        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFolder = API.Instance.Dialogs.SelectFolder();
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
