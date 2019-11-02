using System;

namespace ELO.Models
{
    public class DatabaseConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string DatabaseName { get; set; }
        public Version Version { get; set; } = new Version(8, 0, 18);

        public string ConnectionString() => $"server={Server};database={DatabaseName};user={Username};password={Password};";
    }
}
