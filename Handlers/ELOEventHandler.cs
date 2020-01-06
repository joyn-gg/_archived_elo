using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ELO.Services;
using Microsoft.Extensions.DependencyInjection;
using RavenBOT.Common;
using RavenBOT.Common.TypeReaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ELO.Handlers
{
    public partial class ELOEventHandler
    {
        public ELOEventHandler(ConfigManager configManager, IServiceProvider provider)
        {
            //Ensure lastconfig is populated
            configManager.GetConfig();
            ConfigManager = configManager;
            Logger = provider.GetService<Logger>() ?? new Logger();
            BaseLogger = provider.GetService<LogHandler>() ?? new LogHandler();
            Provider = provider;
            Client = provider.GetRequiredService<DiscordShardedClient>();
            CommandService = provider.GetService<CommandService>() ?? new CommandService();
            ShardChecker = provider.GetService<ShardChecker>() ?? new ShardChecker(Client);
            ShardChecker.AllShardsReady += AllShardsReadyAsync;
            Client.ShardConnected += ShardConnectedAsync;
            Client.ShardReady += ShardReadyAsync;
            //Set commandschedule variables so they don't need to be injected
            CommandSchedule.Provider = provider;
            CommandSchedule.Service = provider.GetRequiredService<CommandService>();
            Client.Log += async x => BaseLogger.Log(x.Message, x.Severity);
            BaseLogger.Message += async (x, y) => Logger.Log(x, y);
        }


        private LogHandler BaseLogger { get; }
        public ConfigManager ConfigManager { get; }
        public Logger Logger { get; }
        public IServiceProvider Provider { get; }
        public DiscordShardedClient Client { get; }
        public CommandService CommandService { get; }
        public ShardChecker ShardChecker { get; }

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
            return Task.CompletedTask;
        }

        public Task ShardReadyAsync(DiscordSocketClient shard)
        {

            Logger.Log($"Shard {shard.ShardId} ready! Guilds:{shard.Guilds.Count} Users:{shard.Guilds.Sum(x => x.MemberCount)}");
            return Task.CompletedTask;
        }

        public async Task JoinedGuildAsync(SocketGuild guild)
        {
            //Check server whitelist
            if (!ConfigManager.LastConfig.IsAcceptable(guild.Id))
            {
                return;
            }

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
                Description = $"Get started by using the help command: `{prefix ?? ConfigManager.LastConfig.Prefix}help`",
                Color = Color.Green
            }.Build());
        }

        public async Task MessageReceivedAsync(SocketMessage discordMessage)
        {

            if (!(discordMessage is SocketUserMessage message))
            {
                return;
            }

            if (ConfigManager.LastConfig.IgnoreBotInput)
            {
                if (message.Author.IsBot || message.Author.IsWebhook)
                {
                    return;
                }
            }
            else
            {
                //Still ignore messages from the bot to avoid recursive commands
                if (message.Author.Id == Client.CurrentUser.Id)
                {
                    return;
                }
            }

            ulong guildId = 0;
            if (message.Channel is IGuildChannel gChannel)
            {
                guildId = gChannel.GuildId;
            }

            //Ensure the server is whitelisted or whitelist disabled
            if (!ConfigManager.LastConfig.IsAcceptable(guildId))
            {
                return;
            }

            var _ = Task.Run(async () =>
            {
                var context = new ShardedCommandContext(Client, message);
                var argPos = 0;

                if (guildId != 0 && !ConfigManager.LastConfig.Developer)
                {
                    //Check that the message was from a server and try to use a custom set prefix if available.
                    using (var db = new Database())
                    {
                        var comp = db.Competitions.FirstOrDefault(x => x.GuildId == guildId);
                        var prefix = comp?.Prefix ?? ConfigManager.LastConfig.Prefix;
                        if (!message.HasStringPrefix(prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    //If the bot is in developer mode or dms use regular prefix or dev override prefix
                    if (!message.HasStringPrefix(ConfigManager.LastConfig.Developer ? ConfigManager.LastConfig.DeveloperPrefix : ConfigManager.LastConfig.Prefix, ref argPos, StringComparison.InvariantCultureIgnoreCase))
                    {
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


        public virtual async Task CommandExecutedAsync(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
            {
                BaseLogger.Log(context.Message.Content, context);
            }
            else 
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

                if (result is ExecuteResult exResult)
                {
                    BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}\n{exResult.Exception}", context, LogSeverity.Error);
                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                    {
                        Title = $"Command Execution Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                        Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                            "__**Error**__\n" +
                            $"{result.ErrorReason.FixLength(512)}\n" +
                            $"{exResult.Exception}".FixLength(1024),
                        Color = Color.LightOrange
                    }.Build());
                }
                else if (result is PreconditionResult preResult)
                {
                    BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);
                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                    {
                        Title = $"Command Precondition Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                        Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                            "__**Error**__\n" +
                            $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                        Color = Color.LightOrange
                    }.Build());
                }
                else if (result is RuntimeResult runResult)
                {
                    BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);
                    //Post execution result. Ie. returned by developer
                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                    {
                        Title = $"Command Runtime Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                        Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                            "__**Error**__\n" +
                            $"{runResult.Reason.FixLength(512)}\n".FixLength(1024),
                        Color = Color.LightOrange
                    }.Build());
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

                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                    {
                        Title = $"Unknown Command",
                        Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                            $"Similar commands: \n{string.Join("\n", toDisplay.Select(x => x.Item2))}",
                        Color = Color.Red
                    }.Build());
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
                        await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                        {
                            Title = $"Argument Error {result.Error.Value}",
                            Description = $"`{commandInfo.Value.Aliases.First()} {string.Join(" ", commandInfo.Value.Parameters.Select(x => x.ParameterInformation()))}`\n" +
                                $"Message: {context.Message.Content.FixLength(512)}\n" +
                                "__**Error**__\n" +
                                $"{result.ErrorReason.FixLength(512)}",
                            Color = Color.DarkRed

                        }.Build());
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                        {
                            Title = $"Command Parse Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                            Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                                "__**Error**__\n" +
                                $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                            Color = Color.LightOrange
                        }.Build());
                    }
                }
                else
                {
                    BaseLogger.Log($"{context.Message.Content}\n{result.Error}\n{result.ErrorReason}", context, LogSeverity.Error);
                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder
                    {
                        Title = $"Command Error{(result.Error.HasValue ? $": {result.Error.Value}" : "")}",
                        Description = $"Message: {context.Message.Content.FixLength(512)}\n" +
                            "__**Error**__\n" +
                            $"{result.ErrorReason.FixLength(512)}\n".FixLength(1024),
                        Color = Color.LightOrange
                    }.Build());
                }
            }
        }

    }
}

