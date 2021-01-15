using Discord;
using System;
using System.Collections.Generic;

namespace ELO.Services.Reactive
{
    /// <summary>
    /// ReactivePage contains the values for any page in a reactive pager, these will override the defaults defined in the pager.
    /// </summary>
    public class ReactivePage
    {
        public EmbedAuthorBuilder Author { get; set; } = null;

        public string Title { get; set; } = null;

        public string Url { get; set; } = null;

        public string Description { get; set; } = null;

        public string ImageUrl { get; set; } = null;

        public string ThumbnailUrl { get; set; } = null;

        public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

        public EmbedFooterBuilder FooterOverride { get; set; } = null;

        public DateTimeOffset? TimeStamp { get; set; } = null;

        public Color? Color { get; set; } = null;
    }
}