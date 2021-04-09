using Newtonsoft.Json;

namespace IndiegalaLibrary.Models
{
    public class SearchResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("html")]
        public string Html { get; set; }
    }

    public class ResultResponse
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string StoreUrl { get; set; }
    }
}
