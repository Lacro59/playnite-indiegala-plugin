using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class ClientInstalled
    {
        public Target target { get; set; }

        public List<string> path { get; set; }
        public double playtime { get; set; }
        public bool needsUpdate { get; set; }
    }

    public class Target
    {
        public ItemData item_data { get; set; }
        public GameData game_data { get; set; }
    }

    public class ItemData
    {
        public long build_version { get; set; }
        public DateTime date { get; set; }
        public string dev_cover { get; set; }
        public string dev_id { get; set; }
        public string dev_image { get; set; }
        public string id_key_name { get; set; }
        public bool in_collection { get; set; }
        public string name { get; set; }
        public string slugged_name { get; set; }
    }

    public class GameData
    {
        public List<string> categories { get; set; }
        public string description_long { get; set; }
        public string description_short { get; set; }
        public string downloadable_lin { get; set; }
        public string downloadable_mac { get; set; }
        public string downloadable_win { get; set; }
        public string exe_path { get; set; }
        public bool in_collection { get; set; }
        public List<string> os { get; set; }
        public Rating rating { get; set; }
        public List<string> specs { get; set; }
        public string system_requirements { get; set; }
        public List<string> tags { get; set; }
        public long views { get; set; }
        public List<string> youtube_best_video { get; set; }
    }

    public class Rating
    {
        public double avg_rating { get; set; }
        public long count { get; set; }
        public bool voted { get; set; }
    }
}
