using Playnite.SDK.Data;

namespace IndiegalaLibrary.Models
{
    public class SearchResponse
    {
        [SerializationPropertyName("status")]
        public string Status { get; set; }
        [SerializationPropertyName("html")]
        public string Html { get; set; }
    }

    public class ResultResponse
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string StoreUrl { get; set; }
    }
}
