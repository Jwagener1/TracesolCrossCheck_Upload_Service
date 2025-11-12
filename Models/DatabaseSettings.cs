using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracesolCrossCheck_Upload_Service;

public sealed class DatabaseSettings
{
    public string Server { get; set; } = "localhost";
    public string DatabaseName { get; set; } = "TracesolCrossCheck";
    public string Username { get; set; } = "Tracesol";
    public string Password { get; set; } = "Tracesol";
    public string ItemLogTable { get; set; } = "Records";

    // Handy helper for later when you actually connect:
    public string BuildSqlConnectionString() =>
        $"Server={Server};Database={DatabaseName};User Id={Username};Password={Password};TrustServerCertificate=True;";
}