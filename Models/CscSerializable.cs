using Discord;
using Discord.Commands;

namespace ELO.Models
{
    public class CscSerializable
    {
        public bool ThrowOnError { get; set; } = false;
        public bool CaseSensitiveCommands { get; set; } = false;
        public bool IgnoreExtraArgs { get; set; } = false;
        public RunMode DefaultRunMode { get; set; } = RunMode.Async;
        public LogSeverity LogLevel { get; set; } = LogSeverity.Info;

        public CommandServiceConfig ToConfig()
        {
            return new CommandServiceConfig
            {
                ThrowOnError = this.ThrowOnError,
                CaseSensitiveCommands = this.CaseSensitiveCommands,
                IgnoreExtraArgs = this.IgnoreExtraArgs,
                DefaultRunMode = this.DefaultRunMode,
                LogLevel = this.LogLevel
            };
        }
    }
}
