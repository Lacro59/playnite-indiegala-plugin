using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class Category
    {
        public int status { get; set; }
        public string name { get; set; }
        public string id_key_name { get; set; }
        public string slugged_name { get; set; }
        public int type { get; set; }
        public int id { get; set; }
    }

    public class DeveloperRef
    {
        public string username { get; set; }
        public object status { get; set; }
        public MediaRef media_ref { get; set; }
        public string title { get; set; }
        public DateTime created { get; set; }
        public string @namespace { get; set; }
        public SettingsRef settings_ref { get; set; }
        public string id_key_name { get; set; }
        public int id { get; set; }
        public ReferencesRef references_ref { get; set; }
    }

    public class DownloadableVersions
    {
        public string win { get; set; }
        public string mac { get; set; }
        public string lin { get; set; }
    }

    public class MediaRef
    {
        public string cover_img { get; set; }
        public string video { get; set; }
        public int id { get; set; }
        public string background { get; set; }
    }

    public class Medium
    {
        public int status { get; set; }
        public string type { get; set; }
        public int order { get; set; }
        public string video_url { get; set; }
        public string preview_url { get; set; }
        public string filename { get; set; }
    }

    public class O
    {
        public int status { get; set; }
        public object type { get; set; }
        public string id_key_name { get; set; }
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Platforms
    {
        public string name { get; set; }
        public string url { get; set; }
        public string id_key_name { get; set; }
        public object icon_path { get; set; }
        public string slugged_name { get; set; }
        public int type { get; set; }
        public int id { get; set; }
    }

    public class ProductData
    {
        public int is_visible { get; set; }
        public int community_type { get; set; }
        public List<Version> version { get; set; }
        public Rating rating { get; set; }
        public Platforms platforms { get; set; }
        public object exe_path { get; set; }
        public object partner { get; set; }
        public int show_indiegala_header { get; set; }
        public int id { get; set; }
        public int product_views { get; set; }
        public object age_ratings { get; set; }
        public ReleaseMood release_mood { get; set; }
        public int qty_sold { get; set; }
        public double priceGBP { get; set; }
        public Theme theme { get; set; }
        public DateTime last_update { get; set; }
        public DownloadableVersions downloadable_versions { get; set; }
        public int sell_type { get; set; }
        public int adult_content { get; set; }
        public int galaclient_visible_only { get; set; }
        public List<string> rating_to_display { get; set; }
        public ProjectKind project_kind { get; set; }
        public double priceUSD { get; set; }
        public List<Medium> media { get; set; }
        public DateTime token_created { get; set; }
        public string description { get; set; }
        public List<Tag> tags { get; set; }
        public string other_text { get; set; }
        public object args { get; set; }
        public string prod_slugged_name { get; set; }
        public string slugged_name { get; set; }
        public string sys_req { get; set; }
        public string id_key_name { get; set; }
        public double priceEUR { get; set; }
        public List<Category> categories { get; set; }
        public int product_downloads { get; set; }
        public ProductMediaRef product_media_ref { get; set; }
        public object discounted { get; set; }
        public string name { get; set; }
        public string created { get; set; }
        public object sdb_discussion_id { get; set; }
        public object release_date { get; set; }
        public ProjectClass project_class { get; set; }
        public object cwd { get; set; }
        public List<Spec> specs { get; set; }
        public string token { get; set; }
        public DeveloperRef developer_ref { get; set; }
        public int galaclient_only { get; set; }
        public bool in_collection { get; set; }
        public List<O> os { get; set; }
        public int super_status { get; set; }
    }

    public class ProductMediaRef
    {
        public object youtube_best_video_date { get; set; }
        public object youtube_best_video { get; set; }
        public object custom_font_color { get; set; }
        public string cover_img { get; set; }
        public string main_img { get; set; }
        public string theme_data { get; set; }
        public string theme { get; set; }
        public object custom_link_color { get; set; }
        public string json_data { get; set; }
        public int id { get; set; }
        public object custom_background_color { get; set; }
    }

    public class ProjectClass
    {
        public int status { get; set; }
        public string description { get; set; }
        public string id_key_name { get; set; }
        public int id { get; set; }
        public string name { get; set; }
    }

    public class ProjectKind
    {
        public int status { get; set; }
        public string description { get; set; }
        public string id_key_name { get; set; }
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Rating
    {
        public double? avg_rating { get; set; }
        public long? count { get; set; }
        public bool voted { get; set; }
    }

    public class ReferencesRef
    {
        public int id { get; set; }
        public string reference { get; set; }
    }

    public class ReleaseMood
    {
        public int status { get; set; }
        public string description { get; set; }
        public string id_key_name { get; set; }
        public int id { get; set; }
        public string name { get; set; }
    }

    public class ClientGameDetails
    {
        public string status { get; set; }
        public string message { get; set; }
        public ProductData product_data { get; set; }
    }

    public class SettingsRef
    {
        public int checkbox_1 { get; set; }
        public int checkbox_2 { get; set; }
        public int checkbox_3 { get; set; }
        public int checkbox_4 { get; set; }
        public int checkbox_5 { get; set; }
        public int checkbox_6 { get; set; }
        public string user_image { get; set; }
        public string cover_img { get; set; }
        public string social_link_1 { get; set; }
        public string social_link_2 { get; set; }
        public object social_link_3 { get; set; }
        public string meta_description { get; set; }
        public object newsletter_api_url { get; set; }
        public int id { get; set; }
        public object visibility_status { get; set; }
        public object og_description { get; set; }
        public object email_link { get; set; }
        public string other_text { get; set; }
        public int? page_status { get; set; }
        public object sdb_discussion_id { get; set; }
        public object mono_product { get; set; }
        public string main_text { get; set; }
    }

    public class Spec
    {
        public int status { get; set; }
        public string name { get; set; }
        public string id_key_name { get; set; }
        public string slugged_name { get; set; }
        public int type { get; set; }
        public int id { get; set; }
    }

    public class Tag
    {
        public int type { get; set; }
        public string slugged_name { get; set; }
        public object id_key_name { get; set; }
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Theme
    {
    }

    public class Version
    {
        public int status { get; set; }
        public string text { get; set; }
        public int enabled { get; set; }
        public object shortURL { get; set; }
        public string version { get; set; }
        public string product_ref_id { get; set; }
        public DateTime date { get; set; }
        public string os { get; set; }
        public int id { get; set; }
    }


}
