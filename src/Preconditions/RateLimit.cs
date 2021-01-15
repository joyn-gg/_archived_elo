using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ELO.Extensions;

namespace ELO.Preconditions
{
    /// <summary> Used to set behavior of the rate limit </summary>
    [Flags]
    public enum RateLimitFlags
    {
        /// <summary> Set none of the flags. </summary>
        None = 0,

        /// <summary> Set whether or not there is no limit to the command in DMs. </summary>
        NoLimitInDMs = 1 << 0,

        /// <summary> Set whether or not there is no limit to the command for guild admins. </summary>
        NoLimitForAdmins = 1 << 1,

        /// <summary> Set whether or not to apply a limit per guild. </summary>
        ApplyPerGuild = 1 << 2
    }

    /// <summary> Sets the scale of the period parameter. </summary>
    public enum Measure
    {
        /// <summary> Period is measured in days. </summary>
        Days,

        /// <summary> Period is measured in hours. </summary>
        Hours,

        /// <summary> Period is measured in minutes. </summary>
        Minutes,

        /// <summary> Period is measured in seconds. </summary>
        Seconds
    }

    /// <summary>
    ///     Sets how often a user is allowed to use this command
    ///     or any command in this module.
    /// </summary>
    /// <remarks>
    ///     This is backed by an in-memory collection
    ///     and will not persist with restarts.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class RateLimitAttribute : PreconditionAttribute
    {
        private readonly bool applyPerGuild;

        private readonly uint invokeLimit;

        private readonly TimeSpan invokeLimitPeriod;

        private readonly Dictionary<(ulong, ulong?), CommandTimeout> invokeTracker = new Dictionary<(ulong, ulong?), CommandTimeout>();

        private readonly bool noLimitForAdmins;

        private readonly bool noLimitInDMs;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitAttribute" /> class.  Sets how often a user is allowed to use
        ///     this command.
        /// </summary>
        /// <param name="times">
        ///     The number of times a user may use the command within a certain period.
        /// </param>
        /// <param name="period">
        ///     The amount of time since first invoke a user has until the limit is lifted.
        /// </param>
        /// <param name="measure">
        ///     The scale in which the <paramref name="period" /> parameter should be measured.
        /// </param>
        /// <param name="flags">
        ///     Flags to set behavior of the rate limit.
        /// </param>
        public RateLimitAttribute(uint times, double period, Measure measure, RateLimitFlags flags = RateLimitFlags.None)
        {
            invokeLimit = times;
            noLimitInDMs = (flags & RateLimitFlags.NoLimitInDMs) == RateLimitFlags.NoLimitInDMs;
            noLimitForAdmins = (flags & RateLimitFlags.NoLimitForAdmins) == RateLimitFlags.NoLimitForAdmins;
            applyPerGuild = (flags & RateLimitFlags.ApplyPerGuild) == RateLimitFlags.ApplyPerGuild;

            switch (measure)
            {
                case Measure.Days:
                    invokeLimitPeriod = TimeSpan.FromDays(period);
                    break;
                case Measure.Hours:
                    invokeLimitPeriod = TimeSpan.FromHours(period);
                    break;
                case Measure.Minutes:
                    invokeLimitPeriod = TimeSpan.FromMinutes(period);
                    break;
                case Measure.Seconds:
                    invokeLimitPeriod = TimeSpan.FromSeconds(period);
                    break;
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitAttribute" /> class.  Sets how often a user is allowed to use
        ///     this command.
        /// </summary>
        /// <param name="times">
        ///     The number of times a user may use the command within a certain period.
        /// </param>
        /// <param name="period">
        ///     The amount of time since first invoke a user has until the limit is lifted.
        /// </param>
        /// <param name="flags">
        ///     Flags to set behavior of the rate limit.
        /// </param>
        public RateLimitAttribute(uint times, TimeSpan period, RateLimitFlags flags = RateLimitFlags.None)
        {
            invokeLimit = times;
            noLimitInDMs = (flags & RateLimitFlags.NoLimitInDMs) == RateLimitFlags.NoLimitInDMs;
            noLimitForAdmins = (flags & RateLimitFlags.NoLimitForAdmins) == RateLimitFlags.NoLimitForAdmins;
            applyPerGuild = (flags & RateLimitFlags.ApplyPerGuild) == RateLimitFlags.ApplyPerGuild;

            invokeLimitPeriod = period;
        }

        /// <inheritdoc />
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (noLimitInDMs && context.Channel is IPrivateChannel)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            if (noLimitForAdmins && context.User is IGuildUser gu && gu.GuildPermissions.Administrator)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            var now = DateTime.UtcNow;
            var key = applyPerGuild ? (context.User.Id, context.Guild?.Id) : (context.User.Id, null);

            var timeout = (invokeTracker.TryGetValue(key, out var t) && ((now - t.FirstInvoke) < invokeLimitPeriod)) ? t : new CommandTimeout(now);

            timeout.TimesInvoked++;

            if (timeout.TimesInvoked <= invokeLimit)
            {
                invokeTracker[key] = timeout;
                return Task.FromResult(PreconditionResult.FromSuccess());
            }

            return Task.FromResult(PreconditionResult.FromError($"You are currently in Timeout for {(timeout.FirstInvoke - DateTime.UtcNow + invokeLimitPeriod).GetReadableLength()}"));
        }

        /*
        public override string PreviewText() => $"Limits command usage to {invokeLimit} uses per {invokeLimitPeriod.GetReadableLength()}";

        public override string Name() => $"RateLimit Precondition";
        */

        /// <summary>
        ///     The command timeout.
        /// </summary>
        private sealed class CommandTimeout
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="CommandTimeout" /> class.
            /// </summary>
            /// <param name="timeStarted">
            ///     The time started.
            /// </param>
            public CommandTimeout(DateTime timeStarted)
            {
                FirstInvoke = timeStarted;
            }

            /// <summary>
            ///     Gets the first invoke.
            /// </summary>
            public DateTime FirstInvoke { get; }

            /// <summary>
            ///     Gets or sets the times invoked.
            /// </summary>
            public uint TimesInvoked { get; set; }
        }
    }
}