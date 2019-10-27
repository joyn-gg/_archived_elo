using System.Collections.Generic;
namespace RavenBOT.ELO.Modules.Modules
{
    //This may be used in future to give proper support for different types of lobbies
    //Such as ones with more than two teams
    public interface EloLobby
    {
         ulong ChannelId { get; set; }
         ulong GuildId { get; set; }

         HashSet<ulong> Queue { get; set; }
         int CurrentGameCount { get; set; } 

    }
}