using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Services.Reactive
{
    public class ReactivePagerCallback : IReactiveCallback
    {
        public ReactivePagerCallback(ReactivePager pager, TimeSpan? timeout = null)
        {
            _timeout = timeout;
            Pager = pager;
            Callbacks = new Dictionary<IEmote, Func<ReactivePagerCallback, SocketReaction, Task<bool>>>();
            pages = Pager.Pages.Count();
        }
        public RunMode RunMode => RunMode.Async;

        /// <summary>
        /// Return false if the precondition does not pass, used to add default checks prior to running callbacks.
        /// </summary>
        public Func<ReactivePagerCallback, SocketReaction, Task<bool>> Precondition = null;

        private TimeSpan? _timeout;

        public TimeSpan? Timeout => _timeout;

        /// <summary>
        /// The context of the command when it was initially run.
        /// </summary>
        private SocketCommandContext _context;

        /// <summary>
        /// Refers to the pager being displayed to the user.
        /// Initialized by the 'DisplayAsync' method
        /// </summary>
        public IUserMessage Message;

        /// <summary>
        /// Refers to the context of the command that originally generated the Pager,
        /// NOT the pager itself.
        /// </summary>
        public SocketCommandContext Context => _context;

        public ReactiveService Reactive;

        public ReactivePager Pager { get; }
        public int pages { get; private set; }
        public int page { get; set; } = 1;

        public Dictionary<IEmote, Func<ReactivePagerCallback, SocketReaction, Task<bool>>> Callbacks { get; set; }

        /// <summary>
        /// This method is called whenever the reactive service detects a reaction on 'Message'
        /// </summary>
        /// <param name="reaction"></param>
        /// <returns>
        /// True if the message is to be unsubscribed from the Service.
        /// </returns>
        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            if (Precondition != null)
            {
                var result = await Precondition.Invoke(this, reaction);
                if (!result)
                {
                    _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                    return false;
                }
            }

            /*
            if (!Pager.Pages.Any())
            {
                return true;
            }
            */

            if (!Callbacks.Any())
            {
                return true;
            }

            if (!Callbacks.TryGetValue(reaction.Emote, out var callback))
            {
                return false;
            }

            return await callback.Invoke(this, reaction).ConfigureAwait(false);
        }

        public async Task<bool> NextAsync(SocketReaction reaction)
        {
            if (page >= pages)
                return false;
            ++page;
            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            await RenderAsync().ConfigureAwait(false);
            return false;
        }

        public async Task<bool> PreviousAsync(SocketReaction reaction)
        {
            if (page <= 1)
                return false;
            --page;
            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            await RenderAsync().ConfigureAwait(false);
            return false;
        }

        public async Task<bool> FirstAsync(SocketReaction reaction)
        {
            page = 1;
            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            await RenderAsync().ConfigureAwait(false);
            return false;
        }

        public async Task<bool> LastAsync(SocketReaction reaction)
        {
            page = pages;
            _ = Message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            await RenderAsync().ConfigureAwait(false);
            return false;
        }

        public async Task<bool> TrashAsync()
        {
            await Message.DeleteAsync().ConfigureAwait(false);
            return true;
        }

        private async Task JumpHandler(SocketMessage m, SocketReaction r, TaskCompletionSource<SocketMessage> source)
        {
            if (m.Author.Id != r.UserId) return;

            if (!int.TryParse(m.Content, out int pageNo)) return;

            if (pageNo < 1 || pageNo > pages) return;

            page = pageNo;
            _ = Message.RemoveReactionAsync(r.Emote, r.User.Value);
            await RenderAsync().ConfigureAwait(false);
            source.SetResult(m);
        }

        public HashSet<ulong> JumpRequests = new HashSet<ulong>();

        public async Task<bool> JumpAsync(SocketReaction reaction)
        {
            if (JumpRequests.Contains(reaction.UserId))
            {
                return false;
            }

            try
            {
                JumpRequests.Add(reaction.UserId);

                var src = new TaskCompletionSource<SocketMessage>();
                Task Func(SocketMessage m) => JumpHandler(m, reaction, src);
                _context.Client.MessageReceived += Func;
                var task = await Task.WhenAny(src.Task, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
                _context.Client.MessageReceived -= Func;
                if (task == src.Task)
                {
                    await src.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                JumpRequests.Remove(reaction.UserId);
            }

            return false;
        }

        /// <summary>
        /// Adds a new page to the end of the pager
        /// Does not change the page number.
        /// </summary>
        /// <param name="newPage"></param>
        public void AddPage(ReactivePage newPage)
        {
            Pager.Pages = Pager.Pages.Append(newPage).ToList();
            pages = Pager.Pages.Count();
        }

        /// <summary>
        /// Sets the pages to a new collection
        /// Will default to a blank page if an empty collection is specified
        /// </summary>
        /// <param name="collection"></param>
        public void SetPages(IEnumerable<ReactivePage> collection)
        {
            if (collection.Count() == 0)
            {
                collection = new ReactivePage[] { new ReactivePage() };
            }
            Pager.Pages = collection;
            page = 1;
            pages = collection.Count();
        }

        /// <summary>
        /// Removes the specified page (at index + 1)
        /// Resets the current page to the first one.
        /// </summary>
        /// <param name="pageNumber"></param>
        public void RemovePage(int pageNumber)
        {
            if (pageNumber <= 0 || pageNumber > pages) return;

            var toRemove = Pager.Pages.ElementAtOrDefault(pageNumber - 1);

            //Ignore invalid index.
            if (toRemove == null) return;
            var pageNum = page;
            SetPages(Pager.Pages.Where(x => !x.Equals(toRemove)).ToList());
        }

        public Task RenderAsync()
        {
            var embed = BuildEmbed();
            return Message.ModifyAsync(m => m.Embed = embed);
        }

        protected Embed BuildEmbed()
        {
            ReactivePage current = null;
            if (Pager.Pages.Any())
            {
                current = Pager.Pages.ElementAt(page - 1);
            }

            var builder = new EmbedBuilder
            {
                Author = current?.Author ?? Pager.Author,
                Title = current?.Title ?? Pager.Title,
                Url = current?.Url ?? Pager.Url,
                Description = current?.Description ?? Pager.Description,
                ImageUrl = current?.ImageUrl ?? Pager.ImageUrl,
                Color = current?.Color ?? Pager.Color,
                Fields = current?.Fields ?? Pager.Fields,
                Footer = current?.FooterOverride ?? Pager.FooterOverride ?? new EmbedFooterBuilder
                {
                    Text = $"{page}/{pages}"
                },
                ThumbnailUrl = current?.ThumbnailUrl ?? Pager.ThumbnailUrl,
                Timestamp = current?.TimeStamp ?? Pager.TimeStamp
            };

            return builder.Build();
        }

        /// <summary>
        /// Sends the initial pager message and sets the 'Message' to it's response.
        /// </summary>
        /// <returns></returns>
        public async Task DisplayAsync(ShardedCommandContext context, ReactiveService service)
        {
            _context = context;
            Reactive = service;
            var embed = BuildEmbed();
            var message = await _context.Channel.SendMessageAsync(Pager.Content, embed: embed).ConfigureAwait(false);
            Message = message;

            //Only attempt to add reaction if the bot has permissions to do so.
            var canReact = _context.Guild?.CurrentUser.GetPermissions(context.Channel as SocketTextChannel).AddReactions;

            if (canReact == null || canReact == true)
            {
                if (Callbacks.Any())
                {
                    var _ = Task.Run(async () => await Message.AddReactionsAsync(Callbacks.Select(x => x.Key).ToArray()).ConfigureAwait(false));
                }
            }
        }

        public async Task DisplayAsync(IMessageChannel channel, ShardedCommandContext context, ReactiveService service)
        {
            _context = context;
            Reactive = service;
            var embed = BuildEmbed();
            var message = await channel.SendMessageAsync(Pager.Content, embed: embed).ConfigureAwait(false);
            Message = message;

            //Only attempt to add reaction if the bot has permissions to do so.
            if (_context.Channel.Id == channel.Id)
            {
                var canReact = _context.Guild?.CurrentUser.GetPermissions(context.Channel as SocketTextChannel).AddReactions;
                if (canReact == null || canReact == true)
                {
                    if (Callbacks.Any())
                    {
                        var _ = Task.Run(async () => await Message.AddReactionsAsync(Callbacks.Select(x => x.Key).ToArray()).ConfigureAwait(false));
                    }
                }
            }
            else
            {
                try
                {
                    if (Callbacks.Any())
                    {
                        var _ = Task.Run(async () => await Message.AddReactionsAsync(Callbacks.Select(x => x.Key).ToArray()).ConfigureAwait(false));
                    }
                }
                catch
                {
                    //
                }
            }

        }

        /// <summary>
        /// Builder for Reactive pager.
        /// </summary>
        /// <param name="emote"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public ReactivePagerCallback WithCallback(IEmote emote, Func<ReactivePagerCallback, SocketReaction, Task<bool>> callback)
        {
            Callbacks.Add(emote, callback);
            return this;
        }

        public ReactivePagerCallback WithDefaultPagerCallbacks()
        {
            Callbacks.Add(new Emoji("⏮"), (x, y) => FirstAsync(y));
            Callbacks.Add(new Emoji("◀"), (x, y) => PreviousAsync(y));
            Callbacks.Add(new Emoji("▶"), (x, y) => NextAsync(y));
            Callbacks.Add(new Emoji("⏭"), (x, y) => LastAsync(y));
            Callbacks.Add(new Emoji("⏹"), (x, y) => TrashAsync());
            return this;
        }

        public ReactivePagerCallback WithJump()
        {
            Callbacks.Add(new Emoji("🔢"), (x, y) => JumpAsync(y));
            return this;
        }
    }
}