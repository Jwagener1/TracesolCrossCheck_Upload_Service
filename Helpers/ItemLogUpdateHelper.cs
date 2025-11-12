using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IItemLogUpdateHelper
{
    Task<bool> MarkSentAsync(long id, CancellationToken ct = default);
}

public sealed class ItemLogUpdateHelper : IItemLogUpdateHelper
{
    private readonly IDbConnectionHelper _dbHelper;
    private readonly IOptionsMonitor<DatabaseSettings> _dbOptions;
    private readonly ILogger<ItemLogUpdateHelper> _logger;

    public ItemLogUpdateHelper(
        IDbConnectionHelper dbHelper,
        IOptionsMonitor<DatabaseSettings> dbOptions,
        ILogger<ItemLogUpdateHelper> logger)
    {
        _dbHelper = dbHelper;
        _dbOptions = dbOptions;
        _logger = logger;
    }

    public async Task<bool> MarkSentAsync(long id, CancellationToken ct = default)
    {
        var db = _dbOptions.CurrentValue;
        var table = Bracket("dbo") + "." + Bracket(db.ItemLogTable);
        var sql = $@"UPDATE {table}
SET [Sent] = 1
WHERE [ID] = @id AND [Sent] = 0";

        await using var conn = await _dbHelper.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.BigInt){ Value = id });

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        var success = rows > 0;
        _logger.LogDebug("MarkSent SQL affected {Rows} rows for ID={Id}", rows, id);
        return success;
    }

    private static string Bracket(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
