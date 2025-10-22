using Playnite.SDK.Data;

namespace IndiegalaLibrary.Models
{
    public class SearchResponse
    {
        [SerializationPropertyName("status")]
        public string Status { get; set; }

        [SerializationPropertyName("html")]
        public string Html { get; set; }

        [SerializationPropertyName("count_bundle")]
        public int CountBundle { get; set; }

        [SerializationPropertyName("count_store")]
        public int CountStore { get; set; }

        [SerializationPropertyName("current_country")]
        public string CurrentCountry { get; set; }
    }
}