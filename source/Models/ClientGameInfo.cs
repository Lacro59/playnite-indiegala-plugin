using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class ClientGameInfo
    {
        public string description_short { get; set; }
        public string description_long { get; set; }
        public string system_requirements { get; set; }
        public List<string> categories { get; set; }
        public string downloadable_win { get; set; }
        public string downloadable_mac { get; set; }
        public string downloadable_lin { get; set; }
        public List<string> os { get; set; }
        public long views { get; set; }
        public Rating rating { get; set; }
        public string stars { get; set; }
        public List<string> specs { get; set; }
        public List<string> tags { get; set; }
        public bool in_collection { get; set; }
        public List<string> youtube_best_video { get; set; }
        public string exe_path { get; set; }
    }
}
