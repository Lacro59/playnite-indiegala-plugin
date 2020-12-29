using IndiegalaLibrary.Models;
using IndiegalaLibrary.Services;
using Newtonsoft.Json;
using Playnite.SDK;
using CommonShared;
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
    /// <summary>
    /// Logique d'interaction pour IndiegalaLibrarySearch.xaml
    /// </summary>
    public partial class IndiegalaLibrarySearch : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public ResultResponse resultResponse { get; set; } = new ResultResponse();


        public IndiegalaLibrarySearch(string GameName)
        {
            InitializeComponent();

            SearchElement.Text = GameName;

            if (!GameName.IsNullOrEmpty())
            {
                SearchData();
            }

            // Set Binding data
            DataContext = this;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            resultResponse = (ResultResponse)lbSelectable.SelectedItem;
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
                    dataSearch = IndiegalaAccountClient.SearchGame(GameSearch);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "HowLongToBeat", "Error on SearchData()");
                }
#if DEBUG
                logger.Debug($"IndiegalaLibrary - dataSearch: {JsonConvert.SerializeObject(dataSearch)}");
#endif
                Application.Current.Dispatcher.BeginInvoke((Action)delegate
                {
                    lbSelectable.ItemsSource = dataSearch;
                    lbSelectable.UpdateLayout();

                    PART_DataLoadWishlist.Visibility = Visibility.Collapsed;
                    SelectableContent.IsEnabled = true;
                });
            });
        }

        private void SearchElement_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ButtonSearch_Click(null, null);
            }
        }
    }
}
