using Discord.Commands;
using ELO.Services;
using RavenBOT.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ELO.Preconditions
{
    public class DevWhitelist : PreconditionBase
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            using (var db = new Database())
            {
                if (db.WhitelistedDevelopers.FirstOrDefault(x => x.UserId == context.User.Id) != null)
                {
                    return PreconditionResult.FromSuccess();
                }
            }

            return PreconditionResult.FromError($"You do not have permission to use this command.");
        }

        public override string Name()
        {
            return "Developer Only";
        }

        public override string PreviewText()
        {
            return "This may only be executed by a whitelisted developer.";
        }
    }
}