using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndiegalaLibrary.Models
{
    public class StoreData
    {
        [SerializationPropertyName("id")]
        public int Id { get; set; }

        [SerializationPropertyName("id_key_name")]
        public string IdKeyName { get; set; }

        [SerializationPropertyName("name")]
        public string Name { get; set; }

        [SerializationPropertyName("serial_ref")]
        public string SerialRef { get; set; }

        [SerializationPropertyName("tier")]
        public int Tier { get; set; }

        [SerializationPropertyName("type")]
        public string Type { get; set; }

        [SerializationPropertyName("url")]
        public string Url { get; set; }

        [SerializationPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        [SerializationPropertyName("profile_visibility")]
        public int ProfileVisibility { get; set; }

        [SerializationPropertyName("order")]
        public int Order { get; set; }

        [SerializationPropertyName("bundle_ref")]
        public string BundleRef { get; set; }

        [SerializationPropertyName("enc_serial_id")]
        public string EncSerialId { get; set; }

        [SerializationPropertyName("serial_status")]
        public string SerialStatus { get; set; }

        [SerializationPropertyName("serial")]
        public string Serial { get; set; }

        [SerializationPropertyName("type_id")]
        public string TypeId { get; set; }

        [SerializationPropertyName("app_id")]
        public string AppId { get; set; }

        [SerializationPropertyName("image")]
        public string Image { get; set; }

        [SerializationPropertyName("image_2")]
        public string Image2 { get; set; }
    }
}