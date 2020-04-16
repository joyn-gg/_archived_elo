using Discord;
using System;

namespace ELO.Handlers
{
    public class Logger
    {
        public void Log(string message, LogSeverity level = LogSeverity.Info)
        {
            Console.WriteLine(message);
        }
    }
}
