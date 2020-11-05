using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace ELO.Services.Reactive
{
    /// <summary>
    /// Defines the functionality of a message using reaction callbacks.
    /// </summary>
    public interface IReactiveCallback
    {
        RunMode RunMode { get; }

        SocketCommandContext Context { get; }

        Task<bool> HandleCallbackAsync(SocketReaction reaction);
    }
}
