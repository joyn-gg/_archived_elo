using Discord;
using Discord.WebSocket;
using ELO.Extensions;
using ELO.Models;
using RavenBOT.Common;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class ReactiveMessageService
    {
        public ReactiveMessageService(DiscordShardedClient client, ShardChecker checker, PremiumService premium, UserService userService)
        {
            Client = client;
            Checker = checker;
            Premium = premium;
            UserService = userService; 
            Client.ReactionAdded += Client_ReactionAdded;

        }

        private Task Checker_AllShardsReady()
        {
            return Task.CompletedTask;
        }

        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> messageCache, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.Emote.Name != registrationConfirmEmoji.Name) return;
            if (!reaction.User.IsSpecified) return;
            if (!(channel is SocketTextChannel guildChannel)) return;
            var user = reaction.User.Value;
            if (user.IsBot || user.IsWebhook) return;

            var _ = Task.Run(async () =>
            {
                using (var db = new Database())
                {
                    var config = db.GetOrCreateCompetition(guildChannel.Guild.Id);
                    if (config == null) return;
                    if (messageCache.Id != config.ReactiveMessage) return;

                    if ((user as SocketGuildUser).IsRegistered(out var player))
                    {
                        return;
                    }

                    var limit = Premium.GetRegistrationLimit(guildChannel.Guild.Id);
                    var registered = ((IQueryable<Player>)db.Players).Count(x => x.GuildId == guildChannel.Guild.Id);
                    if (limit < registered)
                    {
                        var maxErrorMsg = await channel.SendMessageAsync($"{user.Mention} - This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations").ConfigureAwait(false);
                        var errTask = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            await maxErrorMsg.DeleteAsync();
                        });
                        return;
                    }
                    player = new Player(guildChannel.Guild.Id, user.Id, user.Username);
                    db.Add(player);
                    db.SaveChanges();

                    var responses = await UserService.UpdateUserAsync(config, player, db.Ranks.Where(x => x.GuildId == guildChannel.Guild.Id).ToArray(), user as SocketGuildUser).ConfigureAwait(false);

                    var responseMsg = await guildChannel.SendMessageAsync($"{user.Mention} - " + config.FormatRegisterMessage(player) + $"\n{string.Join("\n", responses)}");
                    var resTask = Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        await responseMsg.DeleteAsync().ConfigureAwait(false);
                    });
                }
            });
        }
        public DiscordShardedClient Client { get; }
        public ShardChecker Checker { get; }
        public PremiumService Premium { get; }
        public UserService UserService { get; }

        public static readonly Emoji registrationConfirmEmoji = new Emoji("✅");
    }
}
