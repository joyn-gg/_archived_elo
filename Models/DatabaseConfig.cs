using System;

namespace ELO.Models
{
    public class DatabaseConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Server { get; set; }
        public string DatabaseName { get; set; }

        public string ConnectionString() => $"server={Server};database={DatabaseName};user={Username};password={Password};";
        public string PostgresConnectionString() => $"Host={Server};Database={DatabaseName};Username={Username};Password={Password};";
    }
}
