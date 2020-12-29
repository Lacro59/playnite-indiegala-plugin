using CommonPlaynite.Common;
using Playnite.SDK;
using CommonShared;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace IndiegalaLibrary.Views
{
    /// <summary>
    /// Logique d'interaction pour IndiegalaLibraryExeSelection.xaml
    /// </summary>
    public partial class IndiegalaLibraryExeSelection : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        public static ExecutableInfo executableInfo = null;


        public IndiegalaLibraryExeSelection(string PathDirectory)
        {
            InitializeComponent();

            ScanDirectory(PathDirectory);
        }


        private void ScanDirectory(string PathDirectory)
        {
            PART_Load.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            List<ExecutableInfo> executableInfos = new List<ExecutableInfo>();

            var TaskScanFolder = Task.Run(() => 
            {
                try
                {
                    var fileEnumerator = new SafeFileEnumerator(PathDirectory, "*.exe", SearchOption.AllDirectories);
                    foreach (var file in fileEnumerator)
                    {
                        executableInfos.Add(new ExecutableInfo
                        {
                            Name = Path.GetFileName(file.FullName),
                            Path = Path.GetDirectoryName(file.FullName)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "IndiegalaLibrary");
                }

            })
            .ContinueWith(antecedent => 
            {
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    PART_LvExecutables.Items.Clear();
                    PART_LvExecutables.ItemsSource = executableInfos;

                    PART_Load.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                });
            }); 
        }


        private void PART_LvExecutables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PART_Add.IsEnabled = true;
        }


        private void PART_Add_Click(object sender, RoutedEventArgs e)
        {
            executableInfo = (ExecutableInfo)PART_LvExecutables.SelectedItem;
            ((Window)this.Parent).Close();
        }

        private void PART_Cancel_Click(object sender, RoutedEventArgs e)
        {
            ((Window)this.Parent).Close();
        }
    }


    public class ExecutableInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
