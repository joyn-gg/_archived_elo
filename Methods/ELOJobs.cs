using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RavenBOT.ELO.Modules.Models;
using RavenBOT.ELO.Modules.Premium;

namespace RavenBOT.ELO.Modules.Methods
{
    public partial class ELOService 
    {
        public Timer CompetitionUpdateTimer { get; }
        public void UpdateCompetitionSetups(object stateInfo = null)
        {
            var _ = Task.Run(() =>
            {
                var competitions = Database.Query<CompetitionConfig>();
                var allPlayers = Database.Query<Player>().ToArray();
                foreach (var comp in competitions)
                {
                    var memberCount = allPlayers.Count(x => x.GuildId == comp.GuildId);
                    comp.RegistrationCount = memberCount;
                    Database.Store(comp, CompetitionConfig.DocumentName(comp.GuildId));
                }
            });
        }
        
        public readonly Emoji registrationConfirmEmoji = new Emoji("✅");
        public class ReactiveRegistrationMessage
        {
            public static string DocumentName(ulong guildId)
            {
                return $"ELORegisterMessage-{guildId}";
            }

            public ulong GuildId { get; set; }
            public ulong MessageId { get; set; }
        }

        public ReactiveRegistrationMessage GetReactiveRegistrationMessage(ulong guildId)
        {
            return Database.Load<ReactiveRegistrationMessage>(ReactiveRegistrationMessage.DocumentName(guildId));
        }
        public void SaveReactiveRegistrationMessage(ReactiveRegistrationMessage config)
        {
            Database.Store(config, ReactiveRegistrationMessage.DocumentName(config.GuildId));
        }

        private async Task ReactiveRegistration(Cacheable<IUserMessage, ulong> messageCache, ISocketMessageChannel channel, SocketReaction reaction)
        {            
            if (reaction.Emote.Name != registrationConfirmEmoji.Name) return;
            if (!reaction.User.IsSpecified) return;
            if (!(channel is SocketTextChannel guildChannel)) return;
            var user = reaction.User.Value;
            if (user.IsBot || user.IsWebhook) return;
            var config = Database.Load<ReactiveRegistrationMessage>(ReactiveRegistrationMessage.DocumentName(guildChannel.Guild.Id));
            if (config == null) return;
            if (messageCache.Id != config.MessageId) return;

            var competition = GetOrCreateCompetition(guildChannel.Guild.Id);
            if (user.IsRegistered(this, out var player))
            {
                return;
            }

            var limit = Premium.GetRegistrationLimit(Client, guildChannel.Guild);
            if (limit < competition.RegistrationCount)
            {
                var maxErrorMsg = await channel.SendMessageAsync($"{user.Mention} - This server has exceeded the maximum registration count of {limit}, it must be upgraded to premium to allow additional registrations");
                var errTask = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    await maxErrorMsg.DeleteAsync();
                });
                return;
            }
            player = CreatePlayer(guildChannel.Guild.Id, user.Id, user.Username);
            competition.RegistrationCount++;
            SaveCompetition(competition);

            var responses = await UpdateUserAsync(competition, player, user as SocketGuildUser);

            var responseMsg = await guildChannel.SendMessageAsync($"{user.Mention} - " + competition.FormatRegisterMessage(player) + $"\n{string.Join("\n", responses)}");
            var resTask = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    await responseMsg.DeleteAsync();
                });
        }

        public PatreonIntegration Premium { get; }

        private Task ChannelDeleted(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel gChannel))
            {
                return Task.CompletedTask;
            }

            DeleteLobby(gChannel.Guild.Id, gChannel.Id);

            return Task.CompletedTask;
        }
    }
}