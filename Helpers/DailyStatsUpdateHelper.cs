using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TracesolCrossCheck_Upload_Service.Models;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IDailyStatsUpdateHelper
{
    Task UpdateDailyStatsAsync(CancellationToken ct = default);
}

public sealed class DailyStatsUpdateHelper : IDailyStatsUpdateHelper
{
    private readonly IDbConnectionHelper _dbHelper;
    private readonly IOptionsMonitor<DatabaseSettings> _dbOptions;
    private readonly ILogger<DailyStatsUpdateHelper> _logger;

    public DailyStatsUpdateHelper(
        IDbConnectionHelper dbHelper,
        IOptionsMonitor<DatabaseSettings> dbOptions,
        ILogger<DailyStatsUpdateHelper> logger)
    {
        _dbHelper = dbHelper;
        _dbOptions = dbOptions;
        _logger = logger;
    }

    public async Task UpdateDailyStatsAsync(CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["System"] = "DB" });

        var db = _dbOptions.CurrentValue;
        var recordsTable = $"{BracketIdentifier("dbo")}.{BracketIdentifier(db.ItemLogTable)}";
        var statsTable = $"{BracketIdentifier("dbo")}.{BracketIdentifier(db.DailyStatsTable)}";
        
        // Get today's date (date only, no time component)
        var today = DateTime.Today;

        // SQL to count records for today and update/insert into DailyStats
        var sql = $@"
DECLARE @Today DATE = @StatDate;

-- Calculate counts from Records table for today
DECLARE @TotalScans INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today);
DECLARE @SKU_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [SKU] IS NOT NULL);
DECLARE @Pallet_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Pallet_Number] IS NOT NULL);
DECLARE @OCR_Description_1_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [OCR_Description_1] IS NOT NULL);
DECLARE @Quantity_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Quantity] IS NOT NULL);
DECLARE @Batch_Number_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Batch_Number] IS NOT NULL);
DECLARE @Barcode_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Barcode] IS NOT NULL);
DECLARE @OCR_Description_2_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [OCR_Description_2] IS NOT NULL);
DECLARE @Cross_Check_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Cross_Check] = 1);
DECLARE @Label_Printed_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Label_Printed] = 1);
DECLARE @Label_Applied_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Label_Applied] = 1);
DECLARE @Check_Scan_Result_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Check_Scan_Result] IS NOT NULL);
DECLARE @Valid_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Valid] = 1);
DECLARE @Sent_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Sent] = 1);
DECLARE @ImageSent_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [ImageSent] = 1);
DECLARE @Duplicate_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Duplicate] = 1);
DECLARE @Complete_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Complete] = 1);

-- These counts might need adjustment based on your actual logic for IC1, IC2, etc.
-- Assuming IC1_Good_Read means OCR_Description_1 is not null or empty
DECLARE @IC1_Good_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [OCR_Description_1] IS NOT NULL AND [OCR_Description_1] <> '');
DECLARE @IC1_No_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND ([OCR_Description_1] IS NULL OR [OCR_Description_1] = ''));
DECLARE @IC2_Good_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [OCR_Description_2] IS NOT NULL AND [OCR_Description_2] <> '');
DECLARE @IC2_No_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND ([OCR_Description_2] IS NULL OR [OCR_Description_2] = ''));
DECLARE @Cross_Check_Fail_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Cross_Check] = 0);
DECLARE @CheckScan_Good_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND [Check_Scan_Result] IS NOT NULL AND [Check_Scan_Result] <> '');
DECLARE @CheckScan_No_Read_Count INT = (SELECT COUNT(*) FROM {recordsTable} WHERE CAST([DateTimeStamp] AS DATE) = @Today AND ([Check_Scan_Result] IS NULL OR [Check_Scan_Result] = ''));

-- Merge (update if exists, insert if not)
MERGE {statsTable} AS target
USING (SELECT @Today AS StatDate) AS source
ON target.[StatDate] = source.StatDate
WHEN MATCHED THEN
    UPDATE SET
        [TotalScans] = @TotalScans,
        [SKU_Count] = @SKU_Count,
        [Pallet_Count] = @Pallet_Count,
        [OCR_Description_1_Count] = @OCR_Description_1_Count,
        [Quantity_Count] = @Quantity_Count,
        [Batch_Number_Count] = @Batch_Number_Count,
        [Barcode_Count] = @Barcode_Count,
        [OCR_Description_2_Count] = @OCR_Description_2_Count,
        [Cross_Check_Count] = @Cross_Check_Count,
        [Label_Printed_Count] = @Label_Printed_Count,
        [Label_Applied_Count] = @Label_Applied_Count,
        [Check_Scan_Result_Count] = @Check_Scan_Result_Count,
        [Valid_Count] = @Valid_Count,
        [Sent_Count] = @Sent_Count,
        [ImageSent_Count] = @ImageSent_Count,
        [Duplicate_Count] = @Duplicate_Count,
        [Complete_Count] = @Complete_Count,
        [IC1_Good_Read_Count] = @IC1_Good_Read_Count,
        [IC1_No_Read_Count] = @IC1_No_Read_Count,
        [IC2_Good_Read_Count] = @IC2_Good_Read_Count,
        [IC2_No_Read_Count] = @IC2_No_Read_Count,
        [Cross_Check_Fail_Count] = @Cross_Check_Fail_Count,
        [CheckScan_Good_Read_Count] = @CheckScan_Good_Read_Count,
        [CheckScan_No_Read_Count] = @CheckScan_No_Read_Count
WHEN NOT MATCHED THEN
    INSERT ([StatDate], [TotalScans], [SKU_Count], [Pallet_Count], [OCR_Description_1_Count], 
            [Quantity_Count], [Batch_Number_Count], [Barcode_Count], [OCR_Description_2_Count],
            [Cross_Check_Count], [Label_Printed_Count], [Label_Applied_Count], [Check_Scan_Result_Count],
            [Valid_Count], [Sent_Count], [ImageSent_Count], [Duplicate_Count], [Complete_Count],
            [IC1_Good_Read_Count], [IC1_No_Read_Count], [IC2_Good_Read_Count], [IC2_No_Read_Count],
            [Cross_Check_Fail_Count], [CheckScan_Good_Read_Count], [CheckScan_No_Read_Count])
    VALUES (@Today, @TotalScans, @SKU_Count, @Pallet_Count, @OCR_Description_1_Count,
            @Quantity_Count, @Batch_Number_Count, @Barcode_Count, @OCR_Description_2_Count,
            @Cross_Check_Count, @Label_Printed_Count, @Label_Applied_Count, @Check_Scan_Result_Count,
            @Valid_Count, @Sent_Count, @ImageSent_Count, @Duplicate_Count, @Complete_Count,
            @IC1_Good_Read_Count, @IC1_No_Read_Count, @IC2_Good_Read_Count, @IC2_No_Read_Count,
            @Cross_Check_Fail_Count, @CheckScan_Good_Read_Count, @CheckScan_No_Read_Count);
";

        _logger.LogDebug("Executing DailyStats update SQL for date: {Date}", today);

        await using var conn = await _dbHelper.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@StatDate", today);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("DailyStats updated for {Date}. Rows affected: {Rows}", today, rowsAffected);
    }

    private static string BracketIdentifier(string identifier)
    {
        if (identifier is null) return string.Empty;
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }
}
