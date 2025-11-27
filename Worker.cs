using Microsoft.Extensions.Options; // NEW
using System.IO;                    // NEW
using TracesolCrossCheck_Upload_Service.Helpers;
using TracesolCrossCheck_Upload_Service.Models;

namespace TracesolCrossCheck_Upload_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptionsMonitor<UploadServiceSettings> _uploadMonitor; // switched to monitor
        private readonly IOptionsMonitor<DatabaseSettings> _dbMonitor; // switched to monitor
        private readonly IDbConnectionHelper _dbHelper;
        private readonly IItemLogQueryHelper _itemLogQueryHelper;
        private readonly IItemLogCsvWriter _csvWriter;
        private readonly IItemLogUpdateHelper _updateHelper;
        private readonly IDailyStatsUpdateHelper _statsHelper;

        public Worker(
            ILogger<Worker> logger,
            IOptionsMonitor<UploadServiceSettings> upload,
            IOptionsMonitor<DatabaseSettings> db, // NEW
            IDbConnectionHelper dbHelper,
            IItemLogQueryHelper itemLogQueryHelper,
            IItemLogCsvWriter csvWriter,
            IItemLogUpdateHelper updateHelper,
            IDailyStatsUpdateHelper statsHelper
        )
        {
            using var scope = logger.BeginScope(new Dictionary<string, object> { ["System"] = "APP" });

            _logger = logger;
            _uploadMonitor = upload;
            _dbMonitor = db;
            _dbHelper = dbHelper;
            _itemLogQueryHelper = itemLogQueryHelper;
            _csvWriter = csvWriter;
            _updateHelper = updateHelper;
            _statsHelper = statsHelper;

            var dbSettings = _dbMonitor.CurrentValue;
            var uploadSettings = _uploadMonitor.CurrentValue;

            // Sanity check logs
            _logger.LogInformation(
                "DB config loaded: Server={Server}, DB={Db}, ItemLogTable={Tbl}",
                dbSettings.Server, dbSettings.DatabaseName, dbSettings.ItemLogTable
            );

            _logger.LogInformation(
                "Upload config loaded: LocalFilePath={Local}, RemoteFilePath={Remote}, IntervalMs={Interval}",
                uploadSettings.LocalFilePath, uploadSettings.RemoteFilePath, uploadSettings.IntervalMs
            );

            // Test DB connection right after logging settings
            try
            {
                using (_logger.BeginScope(new Dictionary<string, object> { ["System"] = "DB" }))
                {
                    using var conn = _dbHelper.OpenConnectionAsync().GetAwaiter().GetResult();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    var result = cmd.ExecuteScalar();
                    _logger.LogInformation(
                        "DB connection test succeeded. Info={Info}, Result={Result}",
                        _dbHelper.GetSafeConnectionInfo(), result
                    );
                }
            }
            catch (Exception ex)
            {
                using (_logger.BeginScope(new Dictionary<string, object> { ["System"] = "DB" }))
                {
                    _logger.LogError(ex,
                        "DB connection test failed. Info={Info}",
                        _dbHelper.GetSafeConnectionInfo()
                    );
                }
            }

            // Ensure local/remote directories
            if (!string.IsNullOrWhiteSpace(uploadSettings.LocalFilePath))
            {
                Directory.CreateDirectory(uploadSettings.LocalFilePath);
            }
            if (!string.IsNullOrWhiteSpace(uploadSettings.RemoteFilePath))
            {
                try { Directory.CreateDirectory(uploadSettings.RemoteFilePath); }
                catch (Exception ex)
                {
                    using (_logger.BeginScope(new Dictionary<string, object> { ["System"] = "FILE" }))
                    {
                        _logger.LogError(ex, "Failed to create RemoteFilePath at startup: {Path}", uploadSettings.RemoteFilePath);
                    }
                }
            }

            // Watch for changes to ensure folders exist if paths change at runtime
            _uploadMonitor.OnChange(updated =>
            {
                using var scope2 = _logger.BeginScope(new Dictionary<string, object> { ["System"] = "APP" });
                if (!string.IsNullOrWhiteSpace(updated.LocalFilePath))
                {
                    try { Directory.CreateDirectory(updated.LocalFilePath); }
                    catch (Exception ex)
                    {
                        using (_logger.BeginScope(new Dictionary<string, object> { ["System"] = "FILE" }))
                        {
                            _logger.LogError(ex, "Failed to create LocalFilePath at runtime: {Path}", updated.LocalFilePath);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(updated.RemoteFilePath))
                {
                    try { Directory.CreateDirectory(updated.RemoteFilePath); }
                    catch (Exception ex)
                    {
                        using (_logger.BeginScope(new Dictionary<string, object> { ["System"] = "FILE" }))
                        {
                            _logger.LogError(ex, "Failed to create RemoteFilePath at runtime: {Path}", updated.RemoteFilePath);
                        }
                    }
                }
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object> { ["System"] = "APP" });

            while (!stoppingToken.IsCancellationRequested)
            {
                var upload = _uploadMonitor.CurrentValue;
                var db = _dbMonitor.CurrentValue;

                try
                {
                    var record = await _itemLogQueryHelper.QueryFirstUnsentAsync(stoppingToken);
                    if (record is not null)
                    {
                        _logger.LogInformation(
                            "First unsent record: ID={Id}, SKU={Sku}, Date={Date}",
                            record.ID, record.SKU, record.DateTimeStamp
                        );
                        _logger.LogDebug("Record details: {@Record}", record);

                        // Write CSV locally and copy to remote
                        var path = await _csvWriter.WriteRecordAsync(record, stoppingToken);
                        _logger.LogInformation("Record written to CSV: {Path}", path);

                        // Mark as sent
                        var marked = await _updateHelper.MarkSentAsync(record.ID, stoppingToken);
                        if (marked)
                        {
                            _logger.LogInformation("Record {Id} marked as sent.", record.ID);
                            
                            // Update DailyStats after the record has been marked as sent
                            try
                            {
                                await _statsHelper.UpdateDailyStatsAsync(stoppingToken);
                                _logger.LogDebug("DailyStats updated after marking record {Id} as sent", record.ID);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to update DailyStats after marking record {Id} as sent", record.ID);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Record {Id} was not updated (already sent or missing).", record.ID);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing record");
                }

                _logger.LogDebug(
                    "Tick @ {now} (Interval {ms} ms) - targeting table {table}",
                    DateTimeOffset.Now, upload.IntervalMs, db.ItemLogTable
                );

                try
                {
                    await Task.Delay(upload.IntervalMs, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // ignore on shutdown
                }
            }
        }
    }
}
