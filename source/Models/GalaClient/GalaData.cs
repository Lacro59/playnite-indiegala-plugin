using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models.GalaClient
{
    public class GalaData
    {
        [SerializationPropertyName("state")]
        public int State { get; set; }

        [SerializationPropertyName("message")]
        public string Message { get; set; }

        [SerializationPropertyName("data")]
        public Data Data { get; set; }

        [SerializationPropertyName("platform")]
        public string Platform { get; set; }
    }

    public class Content
    {
        [SerializationPropertyName("user_collection")]
        public List<UserCollection> UserCollection { get; set; }
    }

    public class Data
    {
        [SerializationPropertyName("username")]
        public string Username { get; set; }

        [SerializationPropertyName("userimage")]
        public string Userimage { get; set; }

        [SerializationPropertyName("showcase_content")]
        public ShowcaseContent ShowcaseContent { get; set; }

        [SerializationPropertyName("cookies")]
        public List<string> Cookies { get; set; }
    }

    public class ShowcaseContent
    {
        [SerializationPropertyName("status_code")]
        public int StatusCode { get; set; }

        [SerializationPropertyName("status_code_ok")]
        public bool StatusCodeOk { get; set; }

        [SerializationPropertyName("content")]
        public Content Content { get; set; }

        [SerializationPropertyName("error_text")]
        public object ErrorText { get; set; }
    }

    public class UserCollection
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("date")]
        public DateTime Date { get; set; }

        [SerializationPropertyName("prod_name")]
        public string ProdName { get; set; }

        [SerializationPropertyName("super_status")]
        public int SuperStatus { get; set; }

        [SerializationPropertyName("prod_slugged_name")]
        public string ProdSluggedName { get; set; }

        [SerializationPropertyName("tags")]
        public List<Tag> Tags { get; set; }

        [SerializationPropertyName("prod_id_key_name")]
        public string ProdIdKeyName { get; set; }

        [SerializationPropertyName("prod_dev_username")]
        public string ProdDevUsername { get; set; }

        [SerializationPropertyName("prod_dev_namespace")]
        public string ProdDevNamespace { get; set; }

        [SerializationPropertyName("prod_dev_image")]
        public string ProdDevImage { get; set; }

        [SerializationPropertyName("prod_dev_cover")]
        public string ProdDevCover { get; set; }

        [SerializationPropertyName("version")]
        public List<Version> Version { get; set; }

        [SerializationPropertyName("galaclient_only")]
        public int GalaclientOnly { get; set; }
    }
}
