using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class ClientData
    {
        public int state { get; set; }
        public string message { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string username { get; set; }
        public string userimage { get; set; }
        public ShowcaseContent showcase_content { get; set; }
        public List<string> cookies { get; set; }
    }

    public class ShowcaseContent
    {
        public Content content { get; set; }
        public int status_code { get; set; }
        public string error_text { get; set; }
        public bool status_code_ok { get; set; }
    }

    public class Content
    {
        public List<UserCollection> user_collection { get; set; }
    }

    public class UserCollection
    {
        public string prod_dev_namespace { get; set; }
        public string prod_dev_username { get; set; }
        public string prod_dev_cover { get; set; }
        public string prod_dev_image { get; set; }
        public int id { get; set; }
        public List<Version> version { get; set; }
        public int super_status { get; set; }
        public int galaclient_only { get; set; }
        public DateTime date { get; set; }
        public string prod_slugged_name { get; set; }
        public string prod_id_key_name { get; set; }
        public string prod_name { get; set; }
    }

    public class Version
    {
        public int status { get; set; }
        public string product_ref { get; set; }
        public string text { get; set; }
        public int enabled { get; set; }
        public string shortURL { get; set; }
        public double version { get; set; }
        public DateTime date { get; set; }
        public string os { get; set; }
        public long id { get; set; }
    }
}
