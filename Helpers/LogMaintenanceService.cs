using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public sealed class LogMaintenanceService : BackgroundService
{
    private readonly ILogger<LogMaintenanceService> _logger;

    public LogMaintenanceService(ILogger<LogMaintenanceService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var logPath = GetLogPath();
        try
        {
            EnsureDirectoryExists(logPath);
            TruncateIfNewDay(logPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log maintenance initial check failed for {Path}", logPath);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextMidnight();
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                TruncateFile(logPath);
                _logger.LogInformation("Truncated daily log at midnight: {Path}", logPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to truncate daily log: {Path}", logPath);
            }
        }
    }

    private static string GetLogPath()
    {
        var settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "TracesolCrossCheck");
        var logDir = Path.Combine(settingsDir, "Upload_Logs");
        return Path.Combine(logDir, "upload.log");
    }

    private static void EnsureDirectoryExists(string logPath)
    {
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    private static void TruncateIfNewDay(string path)
    {
        if (!File.Exists(path)) return;
        var lastWrite = File.GetLastWriteTime(path).Date;
        var today = DateTime.Now.Date;
        if (lastWrite != today)
        {
            TruncateFile(path);
        }
    }

    private static void TruncateFile(string path)
    {
        // Attempt to truncate even if file is open by Serilog; shared: true allows shared access
        using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        fs.SetLength(0);
        fs.Flush(true);
    }

    private static TimeSpan GetDelayUntilNextMidnight()
    {
        var now = DateTime.Now;
        var next = now.Date.AddDays(1);
        var delay = next - now;
        return delay < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay;
    }
}
