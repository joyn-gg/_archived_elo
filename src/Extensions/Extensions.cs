using System;
using Discord.WebSocket;
using ELO.Models;
using ELO.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace ELO.Extensions
{
    public static class Extensions
    {
        // public static Dictionary<ulong, Dictionary<ulong, bool>> RegistrationCache = new Dictionary<ulong, Dictionary<ulong, bool>>();

        private static object cacheLock = new object();

        public static Dictionary<(ulong guildId, ulong userId), bool> RegCache = new Dictionary<(ulong guildId, ulong userId), bool>();

        public static string FixLength(this string value, int length = 1023)
        {
            if (value.Length > length)
            {
                value = value.Substring(0, length - 3) + "...";
            }

            return value;
        }

        public static IEnumerable<IEnumerable<T>> SplitList<T>(this IEnumerable<T> list, int groupSize = 30)
        {
            var splitList = new List<IEnumerable<T>>();
            for (var i = 0; i < list.Count(); i += groupSize)
            {
                splitList.Add(list.Skip(i).Take(groupSize).ToList());
                //yield return list.Skip(i).Take(groupSize);
            }

            return splitList;
        }

        public static string GetReadableLength(this TimeSpan length)
        {
            int days = (int)length.TotalDays;
            int hours = (int)length.TotalHours - days * 24;
            int minutes = (int)length.TotalMinutes - days * 24 * 60 - hours * 60;
            int seconds = (int)length.TotalSeconds - days * 24 * 60 * 60 - hours * 60 * 60 - minutes * 60;

            return $"{(days > 0 ? $"{days} Day(s) " : "")}{(hours > 0 ? $"{hours} Hour(s) " : "")}{(minutes > 0 ? $"{minutes} Minute(s) " : "")}{(seconds > 0 ? $"{seconds} Second(s)" : "")}";
        }

        public static string DecodeBase64(this string original)
        {
            try
            {
                byte[] data = Convert.FromBase64String(original);
                string decodedString = Encoding.UTF8.GetString(data);
                return decodedString;
            }
            catch
            {
                return original;
            }
        }

        public static List<Tuple<string, K>> GetEnumNameValues<K>()
        {
            //Ensure that the base type is actually an enum
            if (typeof(K).BaseType != typeof(Enum))
            {
                throw new InvalidCastException();
            }

            return Enum.GetNames(typeof(K)).Select(x => new Tuple<string, K>(x, (K)Enum.Parse(typeof(K), x))).ToList();
            //return Enum.GetValues(typeof(K)).Cast<Int32>().ToDictionary(currentItem => Enum.GetName(typeof(K), currentItem));
        }

        public static string[] EnumNames<K>()
        {
            //Ensure that the base type is actually an enum
            if (typeof(K).BaseType != typeof(Enum))
            {
                throw new InvalidCastException();
            }

            return Enum.GetNames(typeof(K));
        }

        public static IEnumerable<IEnumerable<T>> SplitList<T>(this IEnumerable<T> list, Func<T, int> sumComparator, int maxGroupSum)
        {
            var subList = new List<T>();
            int currentSum = 0;

            foreach (var item in list)
            {
                //Get the size of the current item.
                var addedValue = sumComparator(item);

                //Ensure that the current item will fit in a group
                if (addedValue > maxGroupSum)
                {
                    //TODO: add options to skip fields that exceed the length or add them as a solo group rather than just error out
                    throw new InvalidOperationException("A fields value is greater than the maximum group value size.");
                }

                //Add group to splitlist if the new item will exceed the given size.
                if (currentSum + addedValue > maxGroupSum)
                {
                    //splitList.Append(subList);
                    yield return subList;
                    //Clear the current sum and the subList
                    currentSum = 0;
                    subList = new List<T>();
                }

                subList.Add(item);
                currentSum += addedValue;
            }

            //Return any remaining elements
            if (subList.Count != 0)
            {
                yield return subList;
            }
        }

        private static string Truncate(string str, int length)
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

        public static string ParameterUsage(this IEnumerable<ParameterInfo> parameters)
        {
            return string.Join(" ", parameters.Select(x => x.ParameterInformation()));
        }

        public static string ParameterInformation(this ParameterInfo parameter)
        {
            var initial = parameter.Name + (parameter.Summary == null ? "" : $"({parameter.Summary})");
            var isAttributed = false;
            if (parameter.IsOptional)
            {
                initial = $"[{initial} = {parameter.DefaultValue ?? "null"}]";
                isAttributed = true;
            }

            if (parameter.IsMultiple)
            {
                initial = $"|{initial}|";
                isAttributed = true;
            }

            if (parameter.IsRemainder)
            {
                initial = $"...{initial}";
                isAttributed = true;
            }

            if (!isAttributed)
            {
                initial = $"<{initial}>";
            }

            return initial;
        }


        public static async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(this ShardedCommandContext context, ulong[] userIds, Func<ulong, string> message, Embed embed = null)
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


        public static async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(this ShardedCommandContext context, ulong[] userIds, Func<ulong, string> message, Func<ulong, Embed> embed = null)
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

        public static async Task<Dictionary<ulong, IUserMessage>> MessageUsersAsync(this ShardedCommandContext context, ulong[] userIds, string message, Embed embed = null)
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

        public static async Task<IUserMessage> SimpleEmbedAndDeleteAsync(this ShardedCommandContext context, string content, Color? color = null, TimeSpan? timeout = null)
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


        public static IEnumerable<string> GetUserMentionList(IEnumerable<ulong> userIds)
        {
            return userIds.Select(x =>
            {
                return MentionUtils.MentionUser(x);
            });
        }

        public static async Task SimpleEmbedAsync(this ShardedCommandContext ctx, string message, Discord.Color? color = null)
        {
            var embed = QuickEmbed(message, color);
            await ctx.Channel.SendMessageAsync(null, false, embed);
        }

        public static string GetDisplayName(this SocketGuildUser user)
        {
            return user.Nickname ?? user.Username;
        }

        public static int DamerauLavenshteinDistance(this string s, string t)
        {
            var bounds = new { Height = s.Length + 1, Width = t.Length + 1 };

            int[,] matrix = new int[bounds.Height, bounds.Width];

            for (int height = 0; height < bounds.Height; height++) { matrix[height, 0] = height; };
            for (int width = 0; width < bounds.Width; width++) { matrix[0, width] = width; };

            for (int height = 1; height < bounds.Height; height++)
            {
                for (int width = 1; width < bounds.Width; width++)
                {
                    int cost = (s[height - 1] == t[width - 1]) ? 0 : 1;
                    int insertion = matrix[height, width - 1] + 1;
                    int deletion = matrix[height - 1, width] + 1;
                    int substitution = matrix[height - 1, width - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (height > 1 && width > 1 && s[height - 1] == t[width - 2] && s[height - 2] == t[width - 1])
                    {
                        distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);
                    }

                    matrix[height, width] = distance;
                }
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
        }

        public static Embed QuickEmbed(this string message, Discord.Color? color = null)
        {
            return new EmbedBuilder
            {
                Description = message.FixLength(2047),
                Color = color ?? Discord.Color.Blue
            }.Build();
        }

        public static void ClearUserCache(ulong guildId, ulong userId)
        {
            lock (cacheLock)
            {
                var key = (guildId, userId);
                if (RegCache.ContainsKey(key))
                {
                    RegCache.Remove(key);
                }
            }
        }

        public static bool IsRegistered(this SocketGuildUser user)
        {
            var key = (user.Guild.Id, user.Id);

            lock (cacheLock)
            {
                if (RegCache.TryGetValue(key, out bool registered))
                {
                    return registered;
                }
                else
                {
                    using (var db = new Database())
                    {
                        var playerExists = db.Players.Any(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                        RegCache[key] = playerExists;
                        return playerExists;
                    }
                }
            }
        }

        public static bool IsRegistered(this SocketGuildUser user, out Player player)
        {
            var key = (user.Guild.Id, user.Id);
            bool containsKey;
            bool registered;
            lock (cacheLock)
            {
                containsKey = RegCache.TryGetValue(key, out registered);

                // User is not cached so query db and store result.
                if (!containsKey)
                {
                    using (var db = new Database())
                    {
                        player = db.Players.FirstOrDefault(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                    }

                    registered = player != null;
                    RegCache[key] = registered;
                    return registered;
                }

                // Cached user is considered registered
                if (registered)
                {
                    using (var db = new Database())
                    {
                        player = db.Players.FirstOrDefault(x => x.GuildId == user.Guild.Id && x.UserId == user.Id);
                    }

                    // This will refresh the registration status of the user
                    registered = player != null;

                    RegCache[key] = registered;
                    return registered;
                }

                // Cached user is not registered so do not populate.
                player = null;
                return false;
            }
        }
    }
}