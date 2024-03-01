using IndiegalaLibrary.Models;
using IndiegalaLibrary.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace IndiegalaLibrary.Views
{
    /// <summary>
    /// Logique d'interaction pour IndiegalaLibrarySearch.xaml
    /// </summary>
    public partial class IndiegalaLibrarySearch : UserControl
    {
        public ResultResponse DataResponse { get; set; } = new ResultResponse();


        public IndiegalaLibrarySearch(string gameName)
        {
            InitializeComponent();

            SearchElement.Text = gameName;

            if (!gameName.IsNullOrEmpty())
            {
                SearchData();
            }

            DataContext = this;
        }


        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            DataResponse = (ResultResponse)lbSelectable.SelectedItem;
            ((Window)this.Parent).Close();
        }

        private void LbSelectable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonSelect.IsEnabled = true;
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchData();
        }

        private void SearchElement_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ButtonSearch_Click(null, null);
            }
        }


        private void SearchData()
        {
            PART_DataLoadWishlist.Visibility = Visibility.Visible;
            SelectableContent.IsEnabled = false;
            lbSelectable.ItemsSource = null;

            string GameSearch = SearchElement.Text.Trim();
            Task task = Task.Run(() =>
            {
                List<ResultResponse> dataSearch = new List<ResultResponse>();
                try
                {
                    dataSearch = IndiegalaAccountClient.SearchGame(API.Instance, GameSearch);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                }

                Common.LogDebug(true, $"DataSearch: {Serialization.ToJson(dataSearch)}");

                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    lbSelectable.ItemsSource = dataSearch;
                    lbSelectable.UpdateLayout();

                    PART_DataLoadWishlist.Visibility = Visibility.Collapsed;
                    SelectableContent.IsEnabled = true;
                });
            });
        }
    }
}
