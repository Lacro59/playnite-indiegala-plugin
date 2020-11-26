namespace IndiegalaLibrary.Models
{
    public class SearchResponse
    {
        public string status { get; set; }
        public string html { get; set; }
    }

    public class ResultResponse
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string StoreUrl { get; set; }
    }
}
