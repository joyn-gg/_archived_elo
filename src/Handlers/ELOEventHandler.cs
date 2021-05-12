using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ELO.Extensions;

namespace ELO.Handlers
{
    public partial class ELOEventHandler
    {
        public ELOEventHandler(IServiceProvider provider)
        {
            Logger = provider.GetService<Logger>() ?? new Logger();
            BaseLogger = provider.GetService<LogHandler>() ?? new LogHandler();
            Provider = provider;
            Client = provider.GetRequiredService<DiscordShardedClient>();
            CommandService = provider.GetService<CommandService>() ?? new CommandService();
            ShardChecker = provider.GetService<ShardChecker>() ?? new ShardChecker(Client);
            ReactiveMessageService = new ReactiveMessageService(Client, ShardChecker, Provider.GetRequiredService<PremiumService>(), Provider.GetRequiredService<UserService>());

            ShardChecker.AllShardsReady += AllShardsReadyAsync;
            Client.ShardConnected += ShardConnectedAsync;
            Client.ShardReady += ShardReadyAsync;
            Client.Log += Client_Log;

            //Set commandschedule variables so they don't need to be injected
            CommandSchedule.Provider = provider;
            CommandSchedule.Service = provider.GetRequiredService<CommandService>();
            //Client.Log += async x => BaseLogger.Log(x.Message, x.Severity);
            BaseLogger.Message += async (x, y) => Logger.Log(x, y);
        }

        private Task Client_Log(LogMessage arg)
        {
            BaseLogger.Log($"[{arg.Source ?? "UNK"}]{arg.Message}{(arg.Exception != null ? "\n" + arg.Exception : "")}",
                arg.Severity);
            return Task.CompletedTask;
        }

        private SemaphoreSlim userdownloadSem = new SemaphoreSlim(1);
        private HashSet<ulong> guildAvailableQueue = new HashSet<ulong>();
        private Task Client_GuildAvailable(SocketGuild arg)
        {
            _ = Task.Run(async () =>
            {
                if (guildAvailableQueue.Contains(arg.Id))
                {
                    return;
                }

                guildAvailableQueue.Add(arg.Id);
                if (arg.HasAllMembers)
                {
                    return;
                }

                Console.WriteLine($"Guild: {arg.Name} [{arg.Id}] Became Available");
                await userdownloadSem.WaitAsync();

                try
                {
                    if (ShardChecker.AllShardsReadyFired == false)
                    {
                        // Wait until all shards ready
                        while (true)
                        {
                            await Task.Delay(100);
                            if (ShardChecker.AllShardsReadyFired)
                            {
                                break;
                            }
                        }
                    }

                    var delay = Task.Delay(5000);
                    var completeTask = await Task.WhenAny(delay, arg.DownloadUsersAsync());

                    if (completeTask == delay)
                    {
                        Console.WriteLine(
                            $"Guild: {arg.Name} [{arg.Id} Failed to return user list in time, releasing semaphore...");
                    }
                    else
                    {
                        Console.WriteLine($"Guild: {arg.Name} [{arg.Id}] Downloaded Users");
                        if (!arg.HasAllMembers)
                        {
                            Console.WriteLine(
                                $"Guild: {arg.Name} [{arg.Id}] Does not have all members, {arg.Users.Count}/{arg.MemberCount}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    userdownloadSem.Release();
                    guildAvailableQueue.Remove(arg.Id);
                }
            });

            return Task.CompletedTask;
        }

        private LogHandler BaseLogger { get; }

        public Logger Logger { get; }

        public IServiceProvider Provider { get; }

        public DiscordShardedClient Client { get; }

        private static object pcLock = new object();

        public static Dictionary<ulong, string> PrefixCache { get; set; } = new Dictionary<ulong, string>();

        public static void ClearPrefixCache()
        {
            lock (pcLock)
            {
                PrefixCache.Clear();
            }
        }

        public static void UpdatePrefix(ulong guildId, string prefix)
        {
            if (prefix == null)
            {
                prefix = Program.Prefix;
            }

            lock (pcLock)
            {
                PrefixCache[guildId] = prefix;
            }
        }

        public CommandService CommandService { get; }

        public ShardChecker ShardChecker { get; }

        public ReactiveMessageService ReactiveMessageService { get; }

        public Task AllShardsReadyAsync()
        {
            Client.MessageReceived += MessageReceivedAsync;
            Client.JoinedGuild += JoinedGuildAsync;
            Logger.Log("All shards ready, message received and joined guild events are now subscribed.");
            return Task.CompletedTask;
        }

        public async Task InitializeAsync(string token)
        {
            CommandService.AddTypeReader(typeof(Emoji), new EmojiTypeReader());
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();
            await RegisterModulesAsync();
            CommandService.CommandExecuted += CommandExecutedAsync;
            CommandService.Log += async (x) => BaseLogger.Log(x.Message, x.Severity);
        }

        public Task RegisterModulesAsync()
        {
            return CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), Provider);
        }

        public Task ShardConnectedAsync(DiscordSocketClient shard)
        {
            Logger.Log($"Shard {shard.ShardId} connected! Guilds:{shard.Guilds.Count} Users:{shard.Guilds.Sum(x => x.MemberCount)}");
            foreach (var guild in shard.Guilds)
            {
                Client_GuildAvailable(guild).GetAwaiter().GetResult();
            }
            return Task.CompletedTask;
        }

        public Task ShardReadyAsync(DiscordSocketClient shard)
        {
            Logger.Log($"Shard {shard.ShardId} ready! Guilds:{shard.Guilds.Count} Users:{shard.Guilds.Sum(x => x.MemberCount)}");
            return Task.CompletedTask;
        }

        public async Task JoinedGuildAsync(SocketGuild guild)
        {
            await Client_GuildAvailable(guild);
            //Try to find a channel the bot can send messages to with it's current permissions
            var firstChannel = guild.TextChannels.Where(x =>
            {
                var permissions = guild.CurrentUser?.GetPermissions(x);
                return permissions.HasValue ? permissions.Value.ViewChannel && permissions.Value.SendMessages : false;
            }).OrderBy(c => c.Position).FirstOrDefault();

            string prefix = null;
            using (var db = new Database())
            {
                //Use firstordefault to avoid generating a new competition until commands are run ( GetOrCreateCompetition() )
                var compMatch = db.Competitions.FirstOrDefault(x => x.GuildId == guild.Id);
                prefix = compMatch?.Prefix;
            }

            //Let the server know the help command name
            await firstChannel?.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"{Client.CurrentUser.Username}",
                Description = $"Get started by using the help command: `{prefix ?? Program.Prefix}help`",
                Color = Color.Green
            }.Build());
        }

        public async Task MessageReceivedAsync(SocketMessage discordMessage)
        {
            if (!(discordMessage is SocketUserMessage message))
            {
                return;
            }

            //Still ignore messages from the bot to avoid recursive commands
            if (message.Author.Id == Client.CurrentUser.Id)
            {
                return;
            }

            /*
            if (ConfigManager.LastConfig.IgnoreBotInput)
            {
                if (message.Author.IsBot || message.Author.IsWebhook)
                {
                    return;
                }
            }
            else
            {
            }
            */

            ulong guildId = 0;
            if (message.Channel is IGuildChannel gChannel)
            {
                guildId = gChannel.GuildId;
            }

            var _ = Task.Run(async () =>
            {
                var context = new ShardedCommandContext(Client, message);
                var argPos = 0;

                if (guildId != 0)
                {
                    bool hasPrefixCached;
                    string prefix;
                    lock (PrefixCache)
                    {
                        hasPrefixCached = PrefixCache.TryGetValue(guildId, out prefix);
                    }

                    // prefix not found in cache so pull from db
                    if (!hasPrefixCached)
                    {
                        //Check that the message was from a server and try to use a custom set prefix if available.
                        using (var db = new Database())
                        {
                            var comp = db.Competitions.FirstOrDefault(x => x.GuildId == guildId);
                            prefix = comp?.Prefix ?? Program.Prefix;
                            lock (PrefixCache)
                            {
                                PrefixCache[guildId] = prefix;
                            }
                        }
                    }

                    if (!message.HasStringPrefix(prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!message.HasMentionPrefix(context.Client.CurrentUser, ref argPos))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    //If the bot is in developer mode or dms use regular prefix or dev override prefix
                    if (!message.HasStringPrefix(Program.Prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!message.HasMentionPrefix(context.Client.CurrentUser, ref argPos))
                        {
                            return;
                        }
                        return;
                    }
                }

                //NOTE: Since guildId is 0 for dms, they have their own command queue.
                if (!CommandScheduler.ContainsKey(guildId))
                {
                    CommandScheduler[guildId] = new CommandSchedule
                    {
                        GuildId = guildId
                    };
                }

                CommandScheduler[guildId].AddTask(context, argPos);
            });
        }

        private class CommandRatelimitInfo
        {
            public ulong GuildId;

            public ulong UserId;

            public int Count;

            public DateTime LastNotification;
        }

        private Dictionary<ulong, CommandRatelimitInfo> RatelimitMessageChecks = new Dictionary<ulong, CommandRatelimitInfo>();

        public virtual async Task CommandExecutedAsync(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
            {
                BaseLogger.Log(context.Message.Content, context);
            }
            else
            {
                try
                {
                    //Check for if the server has disabled displaying errors
                    if (context.Guild != null)
                    {
                        using (var db = new Database())
                        {
                            var comp = db.GetOrCreateCompetition(context.Guild.Id);
                            if (!comp.DisplayErrors)
                            {
                                return;
                            }
                        }
                    }

                    bool sendMessage = true;
                    if (context.Channel is IGuildChannel ch)
                    {
                        try
                        {
                            IGuildUser guildUser = null;
                            if (context.Guild != null)
                                guildUser = await context.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                            ChannelPermissions perms = guildUser.GetPermissions(ch);

                            if (!perms.Has(ChannelPermission.SendMessages))
                                sendMessage = false;
                        }
                        catch (Exception e)
                        {
                            Logger.Log($"Issue checking channel permissions for user\n{e}", LogSeverity.Error);
                            sendMessage = false;
                        }
                    }

                    Embed embed = null;
                    if (result is ExecuteResult exResult)
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}\n{exResult.Exception}", context, LogSeverity.Error);
                        embed = new EmbedBuilder
                        {
                            Title = $"Command Execution Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                            Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                "__**Error**__\n" +
                                $"{result.ErrorReason.FixLength(512)}\n" +
                                $"{exResult.Exception}".FixLength(1024),
                            Color = Color.LightOrange
                        }.Build();
                    }
                    else if (result is PreconditionResult preResult)
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);

                        bool respond = true;
                        if (preResult.Error.HasValue)
                        {
                            if (preResult.Error.Value == CommandError.UnmetPrecondition)
                            {
                                if (preResult.ErrorReason.Contains("You are currently in Timeout for", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (RatelimitMessageChecks.TryGetValue(context.User.Id, out var messageCheck))
                                    {
                                        // Do checks
                                        messageCheck.Count++;
                                        if (messageCheck.Count > 3)
                                        {
                                            if (DateTime.UtcNow - messageCheck.LastNotification > TimeSpan.FromSeconds(30))
                                            {
                                                // Last notif was more than 30 seconds ago.
                                                messageCheck.Count = 0;
                                                messageCheck.LastNotification = DateTime.UtcNow;
                                            }
                                            else
                                            {
                                                respond = false;
                                            }
                                        }
                                        else
                                        {
                                            messageCheck.LastNotification = DateTime.UtcNow;
                                        }
                                    }
                                    else
                                    {
                                        RatelimitMessageChecks.Add(context.User.Id, new CommandRatelimitInfo
                                        {
                                            GuildId = context.Guild?.Id ?? 0,
                                            UserId = context.User.Id,
                                            Count = 1,
                                            LastNotification = DateTime.UtcNow
                                        });
                                    }
                                }
                            }
                        }

                        if (respond)
                        {
                            embed = new EmbedBuilder
                            {
                                Title = $"Command Precondition Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                                Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                    "__**Error**__\n" +
                                    $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                                Color = Color.LightOrange
                            }.Build();
                        }
                    }
                    else if (result is RuntimeResult runResult)
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);

                        //Post execution result. Ie. returned by developer
                        embed = new EmbedBuilder
                        {
                            Title = $"Command Runtime Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                            Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                "__**Error**__\n" +
                                $"{runResult.Reason.FixLength(512)}\n".FixLength(1024),
                            Color = Color.LightOrange
                        }.Build();
                    }
                    else if (result is SearchResult sResult)
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);

                        //Since it is an error you can assume it's an unknown command as SearchResults will only return an error if not found.
                        var dlDistances = new List<(int, string, CommandInfo)>();
                        foreach (var command in CommandService.Commands)
                        {
                            foreach (var alias in command.Aliases)
                            {
                                var distance = context.Message.Content.DamerauLavenshteinDistance(alias);
                                if (distance == context.Message.Content.Length || distance == alias.Length)
                                {
                                    continue;
                                }

                                dlDistances.Add((distance, alias, command));
                            }
                        }

                        var ordered = dlDistances.OrderBy(x => x.Item1);
                        var toDisplay = new List<(int, string, CommandInfo)>();
                        foreach (var cmd in ordered)
                        {
                            if (toDisplay.Count >= 5) break;
                            var check = await cmd.Item3.CheckPreconditionsAsync(context, Provider);
                            if (check.IsSuccess)
                            {
                                toDisplay.Add(cmd);
                            }
                        }

                        embed = new EmbedBuilder()
                        {
                            Title = $"Unknown Command",
                            Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                $"Similar commands: \n{string.Join("\n", toDisplay.Select(x => x.Item2))}",
                            Color = Color.Red
                        }.Build();
                    }
                    else if (result is ParseResult pResult)
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);

                        //Invalid parese result can be
                        //ParseFailed, "There must be at least one character of whitespace between arguments."
                        //ParseFailed, "Input text may not end on an incomplete escape."
                        //ParseFailed, "A quoted parameter is incomplete."
                        //BadArgCount, "The input text has too few parameters."
                        //BadArgCount, "The input text has too many parameters."
                        //typeReaderResults
                        if (pResult.Error.Value == CommandError.BadArgCount && commandInfo.IsSpecified)
                        {
                            embed = new EmbedBuilder
                            {
                                Title = $"Argument Error {result.Error.Value}",
                                Description = $"`{commandInfo.Value.Aliases.First()} {string.Join(" ", commandInfo.Value.Parameters.Select(x => x.ParameterInformation()))}`\n" +
                                    $"Message: {context.Message.Content.FixLength(512)}\n" +
                                    "__**Error**__\n" +
                                    $"{result.ErrorReason.FixLength(512)}",
                                Color = Color.DarkRed
                            }.Build();
                        }
                        else
                        {
                            embed = new EmbedBuilder
                            {
                                Title = $"Command Parse Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                                Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                    "__**Error**__\n" +
                                    $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                                Color = Color.LightOrange
                            }.Build();
                        }
                    }
                    else
                    {
                        BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);
                        embed = new EmbedBuilder
                        {
                            Title = $"Command Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                            Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                "__**Error**__\n" +
                                $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                            Color = Color.LightOrange
                        }.Build();
                    }

                    if (embed != null && sendMessage)
                    {
                        await context.Channel.SendMessageAsync("", false, embed);
                    }
                }
                catch (Exception e)
                {
                    BaseLogger.Log("Issue logging command error messages to channel.\n" + e.ToString(), context, LogSeverity.Error);
                }
            }
        }
    }
}