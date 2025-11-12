using Microsoft.Extensions.Options; // NEW
using System.IO;                    // NEW

namespace TracesolCrossCheck_Upload_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptionsMonitor<UploadServiceSettings> _uploadMonitor; // switched to monitor
        private readonly IOptionsMonitor<DatabaseSettings> _dbMonitor; // switched to monitor

        public Worker(
            ILogger<Worker> logger,
            IOptionsMonitor<UploadServiceSettings> upload,
            IOptionsMonitor<DatabaseSettings> db // NEW
        )
        {
            _logger = logger;
            _uploadMonitor = upload;
            _dbMonitor = db;

            var dbSettings = _dbMonitor.CurrentValue;
            var uploadSettings = _uploadMonitor.CurrentValue;

            // Sanity check logs
            _logger.LogInformation(
                "DB config loaded: Server={Server}, DB={Db}, ItemLogTable={Tbl}",
                dbSettings.Server, dbSettings.DatabaseName, dbSettings.ItemLogTable
            );

            _logger.LogInformation(
                "Upload config loaded: CsvOutputFolder={Folder}, IntervalMs={Interval}",
                uploadSettings.CsvOutputFolder, uploadSettings.IntervalMs
            );

            if (!string.IsNullOrWhiteSpace(uploadSettings.CsvOutputFolder))
                Directory.CreateDirectory(uploadSettings.CsvOutputFolder);

            // Watch for changes to ensure folder exists if path changes at runtime
            _uploadMonitor.OnChange(updated =>
            {
                if (!string.IsNullOrWhiteSpace(updated.CsvOutputFolder))
                {
                    try { Directory.CreateDirectory(updated.CsvOutputFolder); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create CsvOutputFolder at runtime: {Path}", updated.CsvOutputFolder);
                    }
                }
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var upload = _uploadMonitor.CurrentValue;
                var db = _dbMonitor.CurrentValue;

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
