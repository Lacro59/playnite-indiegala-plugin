using Playnite.SDK.Data;

namespace IndiegalaLibrary.Models
{
    public class StoreBundleResponse
    {
        [SerializationPropertyName("status")]
        public string Status { get; set; }

        [SerializationPropertyName("code")]
        public string Code { get; set; }

        [SerializationPropertyName("html")]
        public string Html { get; set; }
    }
}