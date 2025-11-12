using Microsoft.Extensions.Options; // NEW
using System.IO;                    // NEW

namespace TracesolCrossCheck_Upload_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly UploadServiceSettings _upload;
        private readonly DatabaseSettings _db; // NEW

        public Worker(
            ILogger<Worker> logger,
            IOptions<UploadServiceSettings> upload,
            IOptions<DatabaseSettings> db // NEW
        )
        {
            _logger = logger;
            _upload = upload.Value;
            _db = db.Value;

            // (Optional) sanity check logs
            _logger.LogInformation(
                "DB config loaded: Server={Server}, DB={Db}, ItemLogTable={Tbl}",
                _db.Server, _db.DatabaseName, _db.ItemLogTable
            );

            if (!string.IsNullOrWhiteSpace(_upload.CsvOutputFolder))
                Directory.CreateDirectory(_upload.CsvOutputFolder);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Tick @ {now} (Interval {ms} ms) — writing from {table} eventually…",
                    DateTimeOffset.Now, _upload.IntervalMs, _db.ItemLogTable
                );

                await Task.Delay(_upload.IntervalMs, stoppingToken);
            }
        }
    }
}
