using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace ELO.Handlers
{
    public class LogHandler
    {
        public event Func<string, LogSeverity, Task> Message;

        public Color LogSeverityAsColor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Info:
                    return Color.Blue;
                case LogSeverity.Critical:
                    return Color.DarkRed;
                case LogSeverity.Error:
                    return Color.Red;
                case LogSeverity.Warning:
                    return Color.Gold;
                case LogSeverity.Debug:
                    return Color.DarkerGrey;
                case LogSeverity.Verbose:
                    return Color.Green;
                default:
                    return Color.Purple;
            }
        }

        private string MakeLogMessage(string message, LogSeverity severity)
        {
            var newSev = severity.ToString().PadRight(20).Substring(0, 4).ToUpper();
            return $"[{newSev}][{DateTime.UtcNow.ToShortDateString()} {DateTime.UtcNow.ToShortTimeString()}] {message}";
        }

        /// <summary>
        /// Log without command context
        /// </summary>
        /// <param name="message"></param>
        /// <param name="severity"></param>
        /// <param name="additional">Additional object, optional</param>
        public virtual void Log(string message, LogSeverity severity = LogSeverity.Info, object additional = null)
        {
            try
            {
                Message.Invoke(MakeLogMessage(message, severity), severity);
            }
            catch (Exception e)
            {
                Console.WriteLine("Logger Error " + e.ToString());
            }
        }

        /// <summary>
        /// log with command context
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="severity"></param>
        /// <param name="additional">Additional object for storage, optional</param>
        public virtual void Log(string message, ICommandContext context, LogSeverity severity = LogSeverity.Info)
        {
            var g = $"G:{context.Guild?.Id} [{context.Guild?.Name}]".PadRight(40);
            var u = $"U:{context.User?.Id} [{context.User?.ToString()}]".PadRight(40);
            var c = $"C:{context.Channel?.Id} [{context.Channel?.Name}]".PadRight(40);
            message = $"{g} {u} {c}\n{message}";

            try
            {
                Message.Invoke(MakeLogMessage(message, severity), severity);
            }
            catch (Exception e)
            {
                Console.WriteLine("Logger Error " + e.ToString());
            }
        }
    }
}
