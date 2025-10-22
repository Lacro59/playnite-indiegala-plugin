using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace IndiegalaLibrary.Models.Api
{
    public class ApiGameDetails
    {
        [SerializationPropertyName("status")]
        public string Status { get; set; }

        [SerializationPropertyName("message")]
        public string Message { get; set; }

        [SerializationPropertyName("product_data")]
        public ProductData ProductData { get; set; }
    }

    public class Category
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("type")]
        public int Type { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }
    }

    public class DeveloperRef
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("namespace")]
        public string Namespace { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("username")]
        public string Username { get; set; }

        [SerializationPropertyName("title")]
        public string Title { get; set; }

        [SerializationPropertyName("status")]
        public object Status { get; set; }

        [SerializationPropertyName("created")]
        public DateTime Created { get; set; }

        [SerializationPropertyName("settings_ref")]
        public SettingsRef SettingsRef { get; set; }

        [SerializationPropertyName("media_ref")]
        public MediaRef MediaRef { get; set; }

        [SerializationPropertyName("references_ref")]
        public ReferencesRef ReferencesRef { get; set; }
    }

    public class DownloadableVersions
    {
        [SerializationPropertyName("win")]
        public string Win { get; set; }

        [SerializationPropertyName("mac")]
        public string Mac { get; set; }

        [SerializationPropertyName("lin")]
        public string Lin { get; set; }
    }

    public class MediaRef
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("video")]
        public string Video { get; set; }

        [SerializationPropertyName("cover_img")]
        public string CoverImg { get; set; }

        [SerializationPropertyName("background")]
        public string Background { get; set; }
    }

    public class Medium
    {
        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("type")]
        public string Type { get; set; }

        [SerializationPropertyName("order")]
        public int Order { get; set; }

        [SerializationPropertyName("video_url")]
        public string VideoUrl { get; set; }

        [SerializationPropertyName("preview_url")]
        public string PreviewUrl { get; set; }

        [SerializationPropertyName("filename")]
        public string Filename { get; set; }
    }

    public class O
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("type")]
        public object Type { get; set; }
    }

    public class Platforms
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("type")]
        public int Type { get; set; }

        [SerializationPropertyName("url")]
        public string Url { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("icon_path")]
        public object IconPath { get; set; }
    }

    public class ProductData
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("developer_ref")]
        public DeveloperRef DeveloperRef { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("super_status")]
        public int SuperStatus { get; set; }

        [SerializationPropertyName("is_visible")]
        public int IsVisible { get; set; }

        [SerializationPropertyName("galaclient_only")]
        public int GalaclientOnly { get; set; }

        [SerializationPropertyName("galaclient_visible_only")]
        public int GalaclientVisibleOnly { get; set; }

        [SerializationPropertyName("adult_content")]
        public int AdultContent { get; set; }

        [SerializationPropertyName("last_update")]
        public DateTime LastUpdate { get; set; }

        [SerializationPropertyName("created")]
        public string Created { get; set; }

        [SerializationPropertyName("release_date")]
        public object ReleaseDate { get; set; }

        [SerializationPropertyName("hide_add_to_library")]
        public int HideAddToLibrary { get; set; }

        [SerializationPropertyName("description")]
        public string Description { get; set; }

        [SerializationPropertyName("other_text")]
        public string OtherText { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("sys_req")]
        public string SysReq { get; set; }

        [SerializationPropertyName("show_indiegala_header")]
        public int ShowIndiegalaHeader { get; set; }

        [SerializationPropertyName("sell_type")]
        public int SellType { get; set; }

        [SerializationPropertyName("community_type")]
        public int CommunityType { get; set; }

        [SerializationPropertyName("discounted")]
        public object Discounted { get; set; }

        [SerializationPropertyName("priceEUR")]
        public double PriceEUR { get; set; }

        [SerializationPropertyName("priceUSD")]
        public double PriceUSD { get; set; }

        [SerializationPropertyName("priceGBP")]
        public double PriceGBP { get; set; }

        [SerializationPropertyName("partner")]
        public object Partner { get; set; }

        [SerializationPropertyName("age_ratings")]
        public object AgeRatings { get; set; }

        [SerializationPropertyName("qty_sold")]
        public int QtySold { get; set; }

        [SerializationPropertyName("product_views")]
        public int ProductViews { get; set; }

        [SerializationPropertyName("product_downloads")]
        public int ProductDownloads { get; set; }

        [SerializationPropertyName("exe_path")]
        public string ExePath { get; set; }

        [SerializationPropertyName("cwd")]
        public object Cwd { get; set; }

        [SerializationPropertyName("args")]
        public object Args { get; set; }

        [SerializationPropertyName("sdb_discussion_id")]
        public int? SdbDiscussionId { get; set; }

        [SerializationPropertyName("token")]
        public string Token { get; set; }

        [SerializationPropertyName("token_created")]
        public DateTime TokenCreated { get; set; }

        [SerializationPropertyName("release_mood")]
        public ReleaseMood ReleaseMood { get; set; }

        [SerializationPropertyName("project_class")]
        public ProjectClass ProjectClass { get; set; }

        [SerializationPropertyName("project_kind")]
        public ProjectKind ProjectKind { get; set; }

        [SerializationPropertyName("platforms")]
        public Platforms Platforms { get; set; }

        [SerializationPropertyName("product_media_ref")]
        public ProductMediaRef ProductMediaRef { get; set; }

        [SerializationPropertyName("categories")]
        public List<Category> Categories { get; set; }

        [SerializationPropertyName("tags")]
        public List<Tag> Tags { get; set; }

        [SerializationPropertyName("os")]
        public List<O> Os { get; set; }

        [SerializationPropertyName("version")]
        public List<Version> Version { get; set; }

        [SerializationPropertyName("rating")]
        public Rating Rating { get; set; }

        [SerializationPropertyName("in_collection")]
        public bool InCollection { get; set; }

        [SerializationPropertyName("rating_to_display")]
        public List<string> RatingToDisplay { get; set; }

        [SerializationPropertyName("downloadable_versions")]
        public DownloadableVersions DownloadableVersions { get; set; }

        [SerializationPropertyName("prod_slugged_name")]
        public string ProdSluggedName { get; set; }

        [SerializationPropertyName("media")]
        public List<Medium> Media { get; set; }

        [SerializationPropertyName("theme")]
        public Theme Theme { get; set; }

        [SerializationPropertyName("specs")]
        public List<Spec> Specs { get; set; }
    }

    public class ProductMediaRef
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("main_img")]
        public string MainImg { get; set; }

        [SerializationPropertyName("cover_img")]
        public string CoverImg { get; set; }

        [SerializationPropertyName("json_data")]
        public string JsonData { get; set; }

        [SerializationPropertyName("custom_background_color")]
        public object CustomBackgroundColor { get; set; }

        [SerializationPropertyName("custom_font_color")]
        public object CustomFontColor { get; set; }

        [SerializationPropertyName("custom_link_color")]
        public object CustomLinkColor { get; set; }

        [SerializationPropertyName("theme_data")]
        public string ThemeData { get; set; }

        [SerializationPropertyName("theme")]
        public string Theme { get; set; }

        [SerializationPropertyName("youtube_best_video")]
        public object YoutubeBestVideo { get; set; }

        [SerializationPropertyName("youtube_best_video_date")]
        public object YoutubeBestVideoDate { get; set; }
    }

    public class ProjectClass
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("description")]
        public string Description { get; set; }
    }

    public class ProjectKind
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("description")]
        public string Description { get; set; }
    }

    public class Rating
    {
        [SerializationPropertyName("count")]
        public int? Count { get; set; }

        [SerializationPropertyName("avg_rating")]
        public double? AvgRating { get; set; }

        [SerializationPropertyName("voted")]
        public bool Voted { get; set; }
    }

    public class ReferencesRef
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("reference")]
        public string Reference { get; set; }
    }

    public class ReleaseMood
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("description")]
        public string Description { get; set; }
    }

    public class SettingsRef
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("page_status")]
        public int? PageStatus { get; set; }

        [SerializationPropertyName("main_text")]
        public string MainText { get; set; }

        [SerializationPropertyName("checkbox_1")]
        public int Checkbox1 { get; set; }

        [SerializationPropertyName("checkbox_2")]
        public int Checkbox2 { get; set; }

        [SerializationPropertyName("checkbox_3")]
        public int Checkbox3 { get; set; }

        [SerializationPropertyName("checkbox_4")]
        public int Checkbox4 { get; set; }

        [SerializationPropertyName("checkbox_5")]
        public int Checkbox5 { get; set; }

        [SerializationPropertyName("checkbox_6")]
        public int Checkbox6 { get; set; }

        [SerializationPropertyName("cover_img")]
        public string CoverImg { get; set; }

        [SerializationPropertyName("user_image")]
        public string UserImage { get; set; }

        [SerializationPropertyName("other_text")]
        public string OtherText { get; set; }

        [SerializationPropertyName("email_link")]
        public string EmailLink { get; set; }

        [SerializationPropertyName("social_link_1")]
        public string SocialLink1 { get; set; }

        [SerializationPropertyName("social_link_2")]
        public string SocialLink2 { get; set; }

        [SerializationPropertyName("social_link_3")]
        public string SocialLink3 { get; set; }

        [SerializationPropertyName("visibility_status")]
        public object VisibilityStatus { get; set; }

        [SerializationPropertyName("meta_description")]
        public string MetaDescription { get; set; }

        [SerializationPropertyName("og_description")]
        public string OgDescription { get; set; }

        [SerializationPropertyName("sdb_discussion_id")]
        public int? SdbDiscussionId { get; set; }

        [SerializationPropertyName("newsletter_api_url")]
        public string NewsletterApiUrl { get; set; }

        [SerializationPropertyName("mono_product")]
        public object MonoProduct { get; set; }
    }

    public class Spec
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("type")]
        public int Type { get; set; }

        [SerializationPropertyName("status")]
        public int Status { get; set; }
    }

    public class Tag
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public object IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("slugged_name")]
        public string SluggedName { get; set; }

        [SerializationPropertyName("type")]
        public int Type { get; set; }
    }

    public class Theme
    {
    }

    public class Version
    {
        [SerializationPropertyName("status")]
        public int Status { get; set; }

        [SerializationPropertyName("text")]
        public string Text { get; set; }

        [SerializationPropertyName("enabled")]
        public int Enabled { get; set; }

        [SerializationPropertyName("shortURL")]
        public object ShortURL { get; set; }

        [SerializationPropertyName("version")]
        public string Ver { get; set; }

        [SerializationPropertyName("product_ref_id")]
        public string ProductRefId { get; set; }

        [SerializationPropertyName("date")]
        public DateTime Date { get; set; }

        [SerializationPropertyName("os")]
        public string Os { get; set; }

        [SerializationPropertyName("id")]
        public int Id { get; set; }
    }
}