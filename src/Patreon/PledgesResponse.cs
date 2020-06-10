namespace Patreon.NET
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class PledgesResponse
    {
        [JsonProperty("data")]
        public List<Datum> Data { get; set; }

        [JsonProperty("included")]
        public List<Included> Included { get; set; }

        [JsonProperty("links")]
        public TopLevelLinks Links { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }
    }

    public partial class Datum
    {
        [JsonProperty("attributes")]
        public DatumAttributes Attributes { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("relationships")]
        public DatumRelationships Relationships { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public partial class DatumAttributes
    {
        [JsonProperty("amount_cents")]
        public long AmountCents { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("declined_since")]
        public DateTimeOffset? DeclinedSince { get; set; }

        [JsonProperty("has_shipping_address")]
        public bool HasShippingAddress { get; set; }

        [JsonProperty("is_paused")]
        public bool IsPaused { get; set; }

        [JsonProperty("patron_pays_fees")]
        public bool PatronPaysFees { get; set; }

        [JsonProperty("pledge_cap_cents")]
        public long PledgeCapCents { get; set; }

        [JsonProperty("total_historical_amount_cents")]
        public long TotalHistoricalAmountCents { get; set; }
    }

    public partial class DatumRelationships
    {
        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("creator")]
        public Creator Creator { get; set; }

        [JsonProperty("patron")]
        public Creator Patron { get; set; }

        [JsonProperty("reward")]
        public Creator Reward { get; set; }
    }

    public partial class Address
    {
        [JsonProperty("data")]
        public object Data { get; set; }
    }

    public partial class Creator
    {
        [JsonProperty("data")]
        public Dat Data { get; set; }

        [JsonProperty("links")]
        public CreatorLinks Links { get; set; }
    }

    public partial class Dat
    {
        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public partial class CreatorLinks
    {
        [JsonProperty("related")]
        public Uri Related { get; set; }
    }

    public partial class Included
    {
        [JsonProperty("attributes")]
        public IncludedAttributes Attributes { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("relationships", NullValueHandling = NullValueHandling.Ignore)]
        public IncludedRelationships Relationships { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public partial class IncludedAttributes
    {
        [JsonProperty("about")]
        public string About { get; set; }

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? Created { get; set; }

        [JsonProperty("default_country_code")]
        public string DefaultCountryCode { get; set; }

        [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; set; }

        [JsonProperty("facebook")]
        public object Facebook { get; set; }

        [JsonProperty("first_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; set; }

        [JsonProperty("full_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FullName { get; set; }

        [JsonProperty("gender", NullValueHandling = NullValueHandling.Ignore)]
        public long? Gender { get; set; }

        [JsonProperty("image_url")]
        public Uri ImageUrl { get; set; }

        [JsonProperty("is_email_verified", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsEmailVerified { get; set; }

        [JsonProperty("last_name", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; set; }

        [JsonProperty("patron_currency")]
        public string PatronCurrency { get; set; }

        [JsonProperty("social_connections", NullValueHandling = NullValueHandling.Ignore)]
        public SocialConnections SocialConnections { get; set; }

        [JsonProperty("thumb_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ThumbUrl { get; set; }

        [JsonProperty("twitch")]
        public object Twitch { get; set; }

        [JsonProperty("twitter")]
        public object Twitter { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("vanity")]
        public string Vanity { get; set; }

        [JsonProperty("youtube")]
        public object Youtube { get; set; }

        [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
        public long? Amount { get; set; }

        [JsonProperty("amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? AmountCents { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("currency", NullValueHandling = NullValueHandling.Ignore)]
        public string Currency { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("discord_role_ids", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> DiscordRoleIds { get; set; }

        [JsonProperty("edited_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? EditedAt { get; set; }

        [JsonProperty("patron_amount_cents")]
        public long? PatronAmountCents { get; set; }

        [JsonProperty("patron_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? PatronCount { get; set; }

        [JsonProperty("post_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? PostCount { get; set; }

        [JsonProperty("published", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Published { get; set; }

        [JsonProperty("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonProperty("remaining")]
        public long? Remaining { get; set; }

        [JsonProperty("requires_shipping", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RequiresShipping { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("unpublished_at")]
        public object UnpublishedAt { get; set; }

        [JsonProperty("user_limit")]
        public object UserLimit { get; set; }

        [JsonProperty("welcome_message")]
        public object WelcomeMessage { get; set; }

        [JsonProperty("welcome_message_unsafe")]
        public object WelcomeMessageUnsafe { get; set; }

        [JsonProperty("welcome_video_embed")]
        public object WelcomeVideoEmbed { get; set; }

        [JsonProperty("welcome_video_url")]
        public object WelcomeVideoUrl { get; set; }

        [JsonProperty("can_see_nsfw", NullValueHandling = NullValueHandling.Ignore)]
        public bool? CanSeeNsfw { get; set; }

        [JsonProperty("discord_id", NullValueHandling = NullValueHandling.Ignore)]
        public string DiscordId { get; set; }

        [JsonProperty("facebook_id")]
        public object FacebookId { get; set; }

        [JsonProperty("google_id", NullValueHandling = NullValueHandling.Ignore)]
        public string GoogleId { get; set; }

        [JsonProperty("has_password", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasPassword { get; set; }

        [JsonProperty("is_deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsDeleted { get; set; }

        [JsonProperty("is_nuked", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsNuked { get; set; }

        [JsonProperty("is_suspended", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSuspended { get; set; }

        [JsonProperty("avatar_photo_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri AvatarPhotoUrl { get; set; }

        [JsonProperty("cover_photo_url")]
        public Uri CoverPhotoUrl { get; set; }

        [JsonProperty("creation_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreationCount { get; set; }

        [JsonProperty("creation_name")]
        public string CreationName { get; set; }

        [JsonProperty("discord_server_id", NullValueHandling = NullValueHandling.Ignore)]
        public string DiscordServerId { get; set; }

        [JsonProperty("display_patron_goals", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DisplayPatronGoals { get; set; }

        [JsonProperty("earnings_visibility", NullValueHandling = NullValueHandling.Ignore)]
        public string EarningsVisibility { get; set; }

        [JsonProperty("image_small_url")]
        public Uri ImageSmallUrl { get; set; }

        [JsonProperty("is_charge_upfront", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsChargeUpfront { get; set; }

        [JsonProperty("is_charged_immediately", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsChargedImmediately { get; set; }

        [JsonProperty("is_monthly", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsMonthly { get; set; }

        [JsonProperty("is_nsfw", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsNsfw { get; set; }

        [JsonProperty("is_plural", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPlural { get; set; }

        [JsonProperty("main_video_embed")]
        public object MainVideoEmbed { get; set; }

        [JsonProperty("main_video_url")]
        public object MainVideoUrl { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("one_liner")]
        public object OneLiner { get; set; }

        [JsonProperty("outstanding_payment_amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? OutstandingPaymentAmountCents { get; set; }

        [JsonProperty("pay_per_name", NullValueHandling = NullValueHandling.Ignore)]
        public string PayPerName { get; set; }

        [JsonProperty("pledge_sum", NullValueHandling = NullValueHandling.Ignore)]
        public long? PledgeSum { get; set; }

        [JsonProperty("pledge_url", NullValueHandling = NullValueHandling.Ignore)]
        public string PledgeUrl { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("thanks_embed")]
        public object ThanksEmbed { get; set; }

        [JsonProperty("thanks_msg", NullValueHandling = NullValueHandling.Ignore)]
        public string ThanksMsg { get; set; }

        [JsonProperty("thanks_video_url")]
        public object ThanksVideoUrl { get; set; }

        [JsonProperty("completed_percentage", NullValueHandling = NullValueHandling.Ignore)]
        public long? CompletedPercentage { get; set; }

        [JsonProperty("reached_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? ReachedAt { get; set; }
    }

    public partial class SocialConnections
    {
        [JsonProperty("deviantart")]
        public object Deviantart { get; set; }

        [JsonProperty("discord")]
        public Discord Discord { get; set; }

        [JsonProperty("facebook")]
        public object Facebook { get; set; }

        [JsonProperty("google")]
        public object Google { get; set; }

        [JsonProperty("instagram")]
        public object Instagram { get; set; }

        [JsonProperty("reddit")]
        public object Reddit { get; set; }

        [JsonProperty("spotify")]
        public object Spotify { get; set; }

        [JsonProperty("twitch")]
        public Twitch Twitch { get; set; }

        [JsonProperty("twitter")]
        public Twitter Twitter { get; set; }

        [JsonProperty("youtube")]
        public Youtube Youtube { get; set; }
    }

    public partial class Discord
    {
        [JsonProperty("url")]
        public object Url { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("scopes", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Scopes { get; set; }
    }

    public partial class Twitch
    {
        [JsonProperty("scopes")]
        public List<string> Scopes { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("user_id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long UserId { get; set; }
    }

    public partial class Twitter
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }

    public partial class Youtube
    {
        [JsonProperty("scopes")]
        public List<Uri> Scopes { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }

    public partial class IncludedRelationships
    {
        [JsonProperty("campaign", NullValueHandling = NullValueHandling.Ignore)]
        public Campaign Campaign { get; set; }

        [JsonProperty("creator", NullValueHandling = NullValueHandling.Ignore)]
        public Creator Creator { get; set; }

        [JsonProperty("goals", NullValueHandling = NullValueHandling.Ignore)]
        public Goals Goals { get; set; }

        [JsonProperty("rewards", NullValueHandling = NullValueHandling.Ignore)]
        public Goals Rewards { get; set; }
    }

    public partial class Campaign
    {
        [JsonProperty("data")]
        public Dat Data { get; set; }

        [JsonProperty("links", NullValueHandling = NullValueHandling.Ignore)]
        public CreatorLinks Links { get; set; }
    }

    public partial class Goals
    {
        [JsonProperty("data")]
        public List<Dat> Data { get; set; }
    }

    public partial class TopLevelLinks
    {
        [JsonProperty("first")]
        public Uri First { get; set; }

        [JsonProperty("next")]
        public Uri Next { get; set; }
    }

    public partial class Meta
    {
        [JsonProperty("count")]
        public long Count { get; set; }
    }

    public partial class PledgesResponse
    {
        public static PledgesResponse FromJson(string json) => JsonConvert.DeserializeObject<PledgesResponse>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this PledgesResponse self) => JsonConvert.SerializeObject(self, Converter.Settings);
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

    internal class ParseStringConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            long l;
            if (Int64.TryParse(value, out l))
            {
                return l;
            }
            throw new Exception("Cannot unmarshal type long");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (long)untypedValue;
            serializer.Serialize(writer, value.ToString());
            return;
        }

        public static readonly ParseStringConverter Singleton = new ParseStringConverter();
    }
}