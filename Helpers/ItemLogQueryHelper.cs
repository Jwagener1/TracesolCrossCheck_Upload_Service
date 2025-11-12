using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TracesolCrossCheck_Upload_Service.Models;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IItemLogQueryHelper
{
    Task<ItemLogRecord?> QueryFirstUnsentAsync(CancellationToken ct = default);
}

public sealed class ItemLogQueryHelper : IItemLogQueryHelper
{
    private readonly IDbConnectionHelper _dbHelper;
    private readonly IOptionsMonitor<DatabaseSettings> _dbOptions;
    private readonly ILogger<ItemLogQueryHelper> _logger;

    public ItemLogQueryHelper(
        IDbConnectionHelper dbHelper,
        IOptionsMonitor<DatabaseSettings> dbOptions,
        ILogger<ItemLogQueryHelper> logger)
    {
        _dbHelper = dbHelper;
        _dbOptions = dbOptions;
        _logger = logger;
    }

    public async Task<ItemLogRecord?> QueryFirstUnsentAsync(CancellationToken ct = default)
    {
        var db = _dbOptions.CurrentValue;
        var table = $"{BracketIdentifier("dbo")}.{BracketIdentifier(db.ItemLogTable)}";
        var sql = $@"SELECT TOP (1)
      [ID]
      ,[DateTimeStamp]
      ,[SKU]
      ,[Pallet_Number]
      ,[OCR_Description_1]
      ,[Quantity]
      ,[Batch_Number]
      ,[Barcode]
      ,[OCR_Description_2]
      ,[Cross_Check]
      ,[Label_Printed]
      ,[Label_Applied]
      ,[Check_Scan_Result]
      ,[Valid]
      ,[Sent]
      ,[ImageSent]
      ,[Duplicate]
      ,[Complete]
  FROM {table} WITH (READPAST)
  WHERE ([Sent] = 0)
  ORDER BY [ID] ASC";

        _logger.LogDebug("Executing SQL: {Sql}", sql);

        await using var conn = await _dbHelper.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapItemLogRecord(reader);
        }
        return null;
    }

    private static string BracketIdentifier(string identifier)
    {
        if (identifier is null) return string.Empty;
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private static ItemLogRecord MapItemLogRecord(SqlDataReader reader)
    {
        ItemLogRecord r = new()
        {
            ID = reader.GetInt64(reader.GetOrdinal("ID")),
            DateTimeStamp = reader.GetDateTime(reader.GetOrdinal("DateTimeStamp")),

            SKU = GetStringOrNull(reader, "SKU"),
            Pallet_Number = GetInt32OrNull(reader, "Pallet_Number"),
            OCR_Description_1 = GetStringOrNull(reader, "OCR_Description_1"),
            Quantity = GetInt32OrNull(reader, "Quantity"),
            Batch_Number = GetStringOrNull(reader, "Batch_Number"),
            Barcode = GetStringOrNull(reader, "Barcode"),
            OCR_Description_2 = GetStringOrNull(reader, "OCR_Description_2"),

            Cross_Check = reader.GetBoolean(reader.GetOrdinal("Cross_Check")),
            Label_Printed = reader.GetBoolean(reader.GetOrdinal("Label_Printed")),
            Label_Applied = reader.GetBoolean(reader.GetOrdinal("Label_Applied")),

            Check_Scan_Result = GetStringOrNull(reader, "Check_Scan_Result"),

            Valid = reader.GetBoolean(reader.GetOrdinal("Valid")),
            Sent = reader.GetBoolean(reader.GetOrdinal("Sent")),
            ImageSent = reader.GetBoolean(reader.GetOrdinal("ImageSent")),
            Duplicate = reader.GetBoolean(reader.GetOrdinal("Duplicate")),
            Complete = reader.GetBoolean(reader.GetOrdinal("Complete")),
        };

        return r;
    }

    private static string? GetStringOrNull(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetString(i);
        }

    private static int? GetInt32OrNull(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetInt32(i);
    }

    private static DateTime? GetDateTimeOrNull(SqlDataReader reader, string column)
    {
        int i = reader.GetOrdinal(column);
        return reader.IsDBNull(i) ? null : reader.GetDateTime(i);
    }
}
