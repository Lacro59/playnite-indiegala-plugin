using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;

namespace IndiegalaLibrary
{
    public class IndiegalaLibrarySettings : ObservableObject
    {
        #region Settings variables
        public bool UseClient { get; set; } = false;

        public int ImageSelectionPriority { get; set; } = 2;

        public bool SelectOnlyWithoutStoreUrl { get; set; } = true;

        public string InstallPath { get; set; }
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        #endregion  
    }


    public class IndiegalaLibrarySettingsViewModel : ObservableObject, ISettings
    {
        private readonly IndiegalaLibrary Plugin;
        private IndiegalaLibrarySettings EditingClone { get; set; }

        private IndiegalaLibrarySettings _Settings;
        public IndiegalaLibrarySettings Settings
        {
            get => _Settings;
            set
            {
                _Settings = value;
                OnPropertyChanged();
            }
        }


        public IndiegalaLibrarySettingsViewModel(IndiegalaLibrary plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<IndiegalaLibrarySettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new IndiegalaLibrarySettings();
            }
        }

        // Code executed when settings view is opened and user starts editing values.
        public void BeginEdit()
        {
            EditingClone = Serialization.GetClone(Settings);
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            Settings = EditingClone;
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Plugin.SavePluginSettings(Settings);
            this.OnPropertyChanged();
        }

        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
