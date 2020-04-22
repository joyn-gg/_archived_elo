using DiscordBotsList.Api.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ELO.Services
{
    public class TopggVoteService
    {
        public TopggVoteService(string token)
        {
            Token = token;
        }

        public string Token { get; }

        public DiscordBotsList.Api.AuthDiscordBotListApi TopClient { get; private set; } = null;

        private static readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        public enum ResultType
        {
            Voted,

            NotVoted,

            NotConfigured
        }

        public async Task<ResultType> CheckAsync(Discord.WebSocket.DiscordShardedClient bot, ulong userId)
        {
            if (Token == null) return ResultType.NotConfigured;
            if (bot.CurrentUser == null) return ResultType.NotConfigured;
            if (TopClient == null)
            {
                TopClient = new DiscordBotsList.Api.AuthDiscordBotListApi(bot.CurrentUser.Id, Token);
            }

            await Locker.WaitAsync();
            try
            {
                if (await TopClient.HasVoted(userId))
                {
                    return ResultType.Voted;
                }

                return ResultType.NotVoted;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return ResultType.NotConfigured;
            }
            finally
            {
                Locker.Release();
            }
        }
    }
}