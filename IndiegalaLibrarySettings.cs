using Newtonsoft.Json;
using Playnite.SDK;
using System.Collections.Generic;

namespace IndiegalaLibrary
{
    public class IndiegalaLibrarySettings : ISettings
    {
        private readonly IndiegalaLibrary plugin;

        public bool EnableCheckVersion { get; set; } = true;

        public bool IsUserLogged { get; set; } = false;

        public int ImageSelectionPriority { get; set; } = 2;

        public bool SelectOnlyWithoutStoreUrl { get; set; } = true;

        public string InstallPath { get; set; }


        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonIgnore` ignore attribute.
        [JsonIgnore]
        public bool OptionThatWontBeSaved { get; set; } = false;


        // Parameterless constructor must exist if you want to use LoadPluginSettings method.
        public IndiegalaLibrarySettings()
        {
        }

        public IndiegalaLibrarySettings(IndiegalaLibrary plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<IndiegalaLibrarySettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                EnableCheckVersion = savedSettings.EnableCheckVersion;

                IsUserLogged = savedSettings.IsUserLogged;

                ImageSelectionPriority = savedSettings.ImageSelectionPriority;

                SelectOnlyWithoutStoreUrl = savedSettings.SelectOnlyWithoutStoreUrl;

                InstallPath = savedSettings.InstallPath;
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}
