using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Data;

namespace IndiegalaLibrary.Models.GalaClient
{
    public class GalaInstalled
    {
        [SerializationPropertyName("target")]
        public Target Target { get; set; }

        [SerializationPropertyName("path")]
        public object Path { get; set; }    // string or List<string>

        [SerializationPropertyName("playtime")]
        public int Playtime { get; set; }

        [SerializationPropertyName("needsUpdate")]
        public bool NeedsUpdate { get; set; }
    }

    public class GameData
    {
        [SerializationPropertyName("description_short")]
        public string DescriptionShort { get; set; }

        [SerializationPropertyName("description_long")]
        public string DescriptionLong { get; set; }

        [SerializationPropertyName("system_requirements")]
        public string SystemRequirements { get; set; }

        [SerializationPropertyName("categories")]
        public List<string> Categories { get; set; }

        [SerializationPropertyName("downloadable_win")]
        public string DownloadableWin { get; set; }

        [SerializationPropertyName("downloadable_mac")]
        public string DownloadableMac { get; set; }

        [SerializationPropertyName("downloadable_lin")]
        public string DownloadableLin { get; set; }

        [SerializationPropertyName("os")]
        public List<string> Os { get; set; }

        [SerializationPropertyName("views")]
        public int Views { get; set; }

        [SerializationPropertyName("rating")]
        public Rating Rating { get; set; }

        [SerializationPropertyName("stars")]
        public string Stars { get; set; }

        [SerializationPropertyName("specs")]
        public List<string> Specs { get; set; }

        [SerializationPropertyName("tags")]
        public List<string> Tags { get; set; }

        [SerializationPropertyName("in_collection")]
        public bool InCollection { get; set; }

        [SerializationPropertyName("youtube_best_video")]
        public string YoutubeBestVideo { get; set; }

        [SerializationPropertyName("exe_path")]
        public string ExePath { get; set; }

        [SerializationPropertyName("cwd")]
        public object Cwd { get; set; }

        [SerializationPropertyName("args")]
        public object Args { get; set; }
    }

    public class ItemData
    {
        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("dev_id")]
        public string DevId { get; set; }

        [SerializationPropertyName("dev_image")]
        public string DevImage { get; set; }

        [SerializationPropertyName("dev_cover")]
        public string DevCover { get; set; }

        [SerializationPropertyName("date")]
        public DateTime Date { get; set; }

        [SerializationPropertyName("in_collection")]
        public bool InCollection { get; set; }

        [SerializationPropertyName("build_version")]
        public string BuildVersion { get; set; }

        [SerializationPropertyName("tags")]
        public List<string> Tags { get; set; }

        [SerializationPropertyName("build_download_path")]
        public object BuildDownloadPath { get; set; }
    }

    public class Target
    {
        [SerializationPropertyName("item_data")]
        public ItemData ItemData { get; set; }

        [SerializationPropertyName("game_data")]
        public GameData GameData { get; set; }
    }
}
