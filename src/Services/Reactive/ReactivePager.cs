using Discord;
using System;
using System.Collections.Generic;

namespace ELO.Services.Reactive
{
    /// <summary>
    /// This defines the base Reactive message, note that values defined in a page will override values defined here if that page is beign displayed
    /// </summary>
    public class ReactivePager
    {
        public ReactivePager() { }
        public ReactivePager(IEnumerable<ReactivePage> pages)
        {
            Pages = pages;
        }

        public IEnumerable<ReactivePage> Pages { get; set; } = new List<ReactivePage>();

        public string Content { get; set; } = string.Empty;

        public EmbedAuthorBuilder Author { get; set; } = null;

        public string Title { get; set; } = null;

        public string Url { get; set; } = null;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = null;

        public string ThumbnailUrl { get; set; } = null;

        public List<EmbedFieldBuilder> Fields { get; set; } = new List<EmbedFieldBuilder>();

        public EmbedFooterBuilder FooterOverride { get; set; } = null;

        public DateTimeOffset? TimeStamp { get; set; } = null;

        public Discord.Color Color { get; set; } = Discord.Color.Default;

        public ReactivePagerCallback ToCallBack(TimeSpan? timeout = null)
        {
            return new ReactivePagerCallback(this, timeout);
        }
    }
}