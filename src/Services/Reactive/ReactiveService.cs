using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ELO.Services.Reactive
{
    public class ReactiveService : IDisposable
    {
        public DiscordShardedClient Client { get; }

        private readonly Dictionary<ulong, IReactiveCallback> callbacks;
        public ReactiveService(DiscordShardedClient client)
        {
            Client = client;
            callbacks = new Dictionary<ulong, IReactiveCallback>();
            Client.ReactionAdded += HandleReactionAsync;
        }

        public virtual void AddReactionCallback(IMessage message, IReactiveCallback callback)
            => callbacks[message.Id] = callback;

        public virtual async Task<IUserMessage> SendPagedMessageAsync(ShardedCommandContext context, ReactivePagerCallback pagerCallback)
        {
            await pagerCallback.DisplayAsync(context, this);
            callbacks.Add(pagerCallback.Message.Id, pagerCallback);
            return pagerCallback.Message;
        }

        public virtual async Task<IUserMessage> SendPagedMessageAsync(ShardedCommandContext context, IMessageChannel channel, ReactivePagerCallback pagerCallback)
        {
            await pagerCallback.DisplayAsync(channel, context, this);
            callbacks.Add(pagerCallback.Message.Id, pagerCallback);
            return pagerCallback.Message;
        }

        public virtual async Task<IUserMessage> ReplyAndDeleteAsync(ShardedCommandContext context, string content, Embed embed, TimeSpan? timeout = null)
        {
            var message = await ReplyAsync(context, content, embed);
            _ = Task.Delay(timeout ?? TimeSpan.FromSeconds(15))
                .ContinueWith(_ => message.DeleteAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
            return message;
        }

        public virtual async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(ShardedCommandContext context, ulong[] userIds, string message, Embed embed = null)
        {
            var responses = new Dictionary<ulong, IUserMessage>();
            foreach (var userId in userIds)
            {
                var user = context.Client.GetUser(userId);
                IUserMessage messageResponse = null;
                if (user != null)
                {
                    try
                    {
                        messageResponse = await user.SendMessageAsync(message, false, embed);
                    }
                    catch
                    {
                        messageResponse = null;
                    }
                }

                responses.Add(userId, messageResponse);
            }

            return responses;
        }

        public virtual async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(ShardedCommandContext context, ulong[] userIds, Func<ulong, string> message, Embed embed = null)
        {
            var responses = new Dictionary<ulong, IUserMessage>();
            foreach (var userId in userIds)
            {
                var user = context.Client.GetUser(userId);
                IUserMessage messageResponse = null;
                if (user != null)
                {
                    try
                    {
                        messageResponse = await user.SendMessageAsync(message(userId), false, embed);
                    }
                    catch
                    {
                        messageResponse = null;
                    }
                }

                responses.Add(userId, messageResponse);
            }

            return responses;
        }

        public virtual async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(ShardedCommandContext context, ulong[] userIds, Func<ulong, string> message, Func<ulong, Embed> embed = null)
        {
            var responses = new Dictionary<ulong, IUserMessage>();
            foreach (var userId in userIds)
            {
                var user = context.Client.GetUser(userId);
                IUserMessage messageResponse = null;
                if (user != null)
                {
                    try
                    {
                        messageResponse = await user.SendMessageAsync(message(userId), false, embed(userId));
                    }
                    catch
                    {
                        messageResponse = null;
                    }
                }

                responses.Add(userId, messageResponse);
            }

            return responses;
        }

        public virtual async Task<IUserMessage> ReplyAsync(ShardedCommandContext context, string message, Embed embed)
        {
            return await context.Channel.SendMessageAsync(message, false, embed);
        }

        public virtual async Task<IUserMessage> ReplyAsync(ShardedCommandContext context, Embed embed)
        {
            return await context.Channel.SendMessageAsync("", false, embed);
        }

        public virtual async Task<IUserMessage> ReplyAsync(ShardedCommandContext context, EmbedBuilder embed)
        {
            return await context.Channel.SendMessageAsync("", false, embed.Build());
        }


        public virtual async Task<IUserMessage> SimpleEmbedAsync(ShardedCommandContext context, string content, Color? color = null)
        {
            var embed = new EmbedBuilder();
            embed.Description = Truncate(content, 2047);
            embed.Color = color ?? Color.Default;
            return await context.Channel.SendMessageAsync("", false, embed.Build());
        }

        public virtual async Task<IUserMessage> SimpleEmbedAndDeleteAsync(ShardedCommandContext context, string content, Color? color = null, TimeSpan? timeout = null)
        {
            var embed = new EmbedBuilder();
            embed.Description = Truncate(content, 2047);
            embed.Color = color ?? Color.Default;
            var message = await context.Channel.SendMessageAsync("", false, embed.Build());
            _ = Task.Delay(timeout ?? TimeSpan.FromSeconds(15))
                .ContinueWith(_ => message.DeleteAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
            return message;
        }

        public virtual async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (Client.CurrentUser == null) return;
            if (reaction == null) return;
            if (reaction.UserId == Client.CurrentUser.Id)
            {
                //Ignore reactions added by the bot itself.
                return;
            }

            if (!callbacks.TryGetValue(message.Id, out var callback))
            {
                //Ensure the message being reacted on is actually one created for the service.
                return;
            }

            switch (callback.RunMode)
            {
                case RunMode.Async:
                    _ = Task.Run(async () =>
                    {
                        if (await callback.HandleCallbackAsync(reaction).ConfigureAwait(false))
                        {
                            callbacks.Remove(message.Id);
                        }
                    });
                    break;
                default:
                    if (await callback.HandleCallbackAsync(reaction).ConfigureAwait(false))
                    {
                        callbacks.Remove(message.Id);
                    }

                    break;
            }
        }

        public virtual async Task<SocketMessage> NextMessageAsync(SocketCommandContext context, Func<SocketCommandContext, SocketMessage, Task<bool>> judge, TimeSpan? timeout = null)
        {
            timeout = timeout ?? TimeSpan.FromSeconds(15);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();

            Task Func(SocketMessage m) => HandleNextMessageAsync(m, context, eventTrigger, judge);

            context.Client.MessageReceived += Func;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            context.Client.MessageReceived -= Func;

            if (task == trigger)
            {
                return await trigger.ConfigureAwait(false);
            }

            return null;
        }

        private static async Task HandleNextMessageAsync(SocketMessage message, SocketCommandContext context, TaskCompletionSource<SocketMessage> eventTrigger, Func<SocketCommandContext, SocketMessage, Task<bool>> judge)
        {
            var result = await judge(context, message).ConfigureAwait(false);
            if (result)
            {
                eventTrigger.SetResult(message);
            }
        }

        public virtual void Dispose()
        {
            Client.ReactionAdded -= HandleReactionAsync;
        }

        private string Truncate(string str, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            }

            if (str == null)
            {
                return null;
            }

            int maxLength = Math.Min(str.Length, length);
            return str.Substring(0, maxLength);
        }
    }
}