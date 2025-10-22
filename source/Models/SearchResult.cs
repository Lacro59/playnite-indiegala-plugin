using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class SearchResult
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string StoreUrl { get; set; }
        public bool IsShowcase { get; set; }
    }
}