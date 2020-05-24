using System;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ELO.Services.Premium
{
    public partial class TopLevel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("role_ids")]
        public string RoleIds { get; set; }

        [JsonProperty("tier_ids")]
        public string TierIds { get; set; }

        [JsonProperty("discord_id")]
        public string DiscordId { get; set; }

        [JsonProperty("last_payment")]
        public DateTimeOffset LastPayment { get; set; }

        [JsonProperty("discord_user_id")]
        public string DiscordUserId { get; set; }

        [JsonProperty("last_payment_status")]
        public string LastPaymentStatus { get; set; }
    }

    public partial class TopLevel
    {
        public static TopLevel FromJson(string json) => JsonConvert.DeserializeObject<TopLevel>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this TopLevel self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}