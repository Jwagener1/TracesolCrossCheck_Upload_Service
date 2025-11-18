using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using TracesolCrossCheck_Upload_Service.Models;
using Microsoft.Extensions.Logging;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IItemLogCsvWriter
{
    // Writes a single record to a CSV file in LocalFilePath, then copies it to RemoteFilePath. Returns the local file path.
    Task<string> WriteRecordAsync(ItemLogRecord record, CancellationToken ct = default);

    // Writes multiple records to a single CSV file (data rows only) in LocalFilePath, then copies it to RemoteFilePath. Returns the local file path.
    Task<string> WriteRecordsAsync(IEnumerable<ItemLogRecord> records, string? fileName = null, CancellationToken ct = default);

    // Produces CSV text; data row only by default
    string ToCsv(ItemLogRecord record, bool includeHeader = false);
}

public sealed class ItemLogCsvWriter : IItemLogCsvWriter
{
    private readonly IOptionsMonitor<UploadServiceSettings> _uploadOptions;
    private readonly ILogger<ItemLogCsvWriter> _logger;

    public ItemLogCsvWriter(IOptionsMonitor<UploadServiceSettings> uploadOptions, ILogger<ItemLogCsvWriter> logger)
    {
        _uploadOptions = uploadOptions;
        _logger = logger;
    }

    public async Task<string> WriteRecordAsync(ItemLogRecord record, CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["System"] = "FILE" });

        var settings = _uploadOptions.CurrentValue;
        var localFolder = settings.LocalFilePath;
        var remoteFolder = settings.RemoteFilePath;

        Directory.CreateDirectory(localFolder);
        if (!string.IsNullOrWhiteSpace(remoteFolder))
        {
            // Ensure destination exists; if invalid path this will throw and be handled by caller
            Directory.CreateDirectory(remoteFolder);
        }

        var fileName = $"record_{record.ID}_{record.DateTimeStamp:yyyyMMdd_HHmmss}.csv";
        var localPath = Path.Combine(localFolder, SanitizeFileName(fileName));

        var csv = ToCsv(record, includeHeader: false); // data only
        await File.WriteAllTextAsync(localPath, csv, Encoding.UTF8, ct);
        _logger.LogInformation("Wrote CSV locally: {Path}", localPath);

        // Copy to remote if configured
        if (!string.IsNullOrWhiteSpace(remoteFolder))
        {
            var remotePath = Path.Combine(remoteFolder, SanitizeFileName(fileName));
            File.Copy(localPath, remotePath, overwrite: true);
            _logger.LogInformation("Copied CSV to remote: {Path}", remotePath);
        }

        return localPath;
    }

    public async Task<string> WriteRecordsAsync(IEnumerable<ItemLogRecord> records, string? fileName = null, CancellationToken ct = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["System"] = "FILE" });

        var list = records as IList<ItemLogRecord> ?? records.ToList();
        if (list.Count == 0)
            throw new InvalidOperationException("No records to write");

        var settings = _uploadOptions.CurrentValue;
        var localFolder = settings.LocalFilePath;
        var remoteFolder = settings.RemoteFilePath;

        Directory.CreateDirectory(localFolder);
        if (!string.IsNullOrWhiteSpace(remoteFolder))
        {
            Directory.CreateDirectory(remoteFolder);
        }

        var name = fileName ?? $"records_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        var localPath = Path.Combine(localFolder, SanitizeFileName(name));

        var sb = new StringBuilder();
        // No header line; write data rows only
        foreach (var r in list)
            sb.AppendLine(ToCsvLine(r));

        await File.WriteAllTextAsync(localPath, sb.ToString(), Encoding.UTF8, ct);
        _logger.LogInformation("Wrote CSV locally: {Path}", localPath);

        if (!string.IsNullOrWhiteSpace(remoteFolder))
        {
            var remotePath = Path.Combine(remoteFolder, SanitizeFileName(name));
            File.Copy(localPath, remotePath, overwrite: true);
            _logger.LogInformation("Copied CSV to remote: {Path}", remotePath);
        }

        return localPath;
    }

    public string ToCsv(ItemLogRecord record, bool includeHeader = false)
    {
        var sb = new StringBuilder();
        if (includeHeader)
        {
            // Header intentionally omitted by default; provide only if explicitly requested
            sb.AppendLine(string.Join(',', new[]
            {
                "ID","DateTimeStamp","SKU","Pallet_Number","OCR_Description_1","Quantity","Batch_Number","Barcode",
                "OCR_Description_2","Cross_Check","Label_Printed","Label_Applied","Check_Scan_Result","Valid","Sent",
                "ImageSent","Duplicate","Complete"
            }));
        }
        sb.AppendLine(ToCsvLine(record));
        return sb.ToString();
    }

    private static string ToCsvLine(ItemLogRecord r)
    {
        // Convert values to CSV-safe strings in the correct column order
        var values = new[]
        {
            r.ID.ToString(CultureInfo.InvariantCulture),
            FormatDate(r.DateTimeStamp),
            Q(r.SKU),
            r.Pallet_Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Q(r.OCR_Description_1),
            r.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            Q(r.Batch_Number),
            Q(r.Barcode),
            Q(r.OCR_Description_2),
            ToBit(r.Cross_Check),
            ToBit(r.Label_Printed),
            ToBit(r.Label_Applied),
            Q(r.Check_Scan_Result),
            ToBit(r.Valid),
            ToBit(r.Sent),
            ToBit(r.ImageSent),
            ToBit(r.Duplicate),
            ToBit(r.Complete)
        };
        return string.Join(',', values);
    }

    private static string ToBit(bool b) => b ? "1" : "0";

    private static string FormatDate(DateTime dt) => dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Q(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        var escaped = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
