using Discord;
using System;
using System.Collections.Generic;
using System.Text;

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
