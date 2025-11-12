using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IDbConnectionHelper
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default);
    string GetSafeConnectionInfo();
}

public sealed class DbConnectionHelper : IDbConnectionHelper
{
    private readonly IOptionsMonitor<DatabaseSettings> _dbOptions;

    public DbConnectionHelper(IOptionsMonitor<DatabaseSettings> dbOptions)
    {
        _dbOptions = dbOptions;
    }

    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connStr = _dbOptions.CurrentValue.BuildSqlConnectionString();
        var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return conn;
    }

    public string GetSafeConnectionInfo()
    {
        var db = _dbOptions.CurrentValue;
        return $"Server={db.Server}; Database={db.DatabaseName}; User Id={Mask(db.Username)}";
    }

    private static string Mask(string? value)
        => string.IsNullOrEmpty(value) ? "" : new string('*', Math.Min(4, value.Length));
}
