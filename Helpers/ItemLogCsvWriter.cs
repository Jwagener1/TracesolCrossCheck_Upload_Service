using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using TracesolCrossCheck_Upload_Service.Models;

namespace TracesolCrossCheck_Upload_Service.Helpers;

public interface IItemLogCsvWriter
{
    // Writes a single record to a CSV file in the configured output folder, returns the file path
    Task<string> WriteRecordAsync(ItemLogRecord record, CancellationToken ct = default);

    // Writes multiple records to a single CSV file (data rows only), returns the file path
    Task<string> WriteRecordsAsync(IEnumerable<ItemLogRecord> records, string? fileName = null, CancellationToken ct = default);

    // Produces CSV text; data row only by default
    string ToCsv(ItemLogRecord record, bool includeHeader = false);
}

public sealed class ItemLogCsvWriter : IItemLogCsvWriter
{
    private readonly IOptionsMonitor<UploadServiceSettings> _uploadOptions;

    public ItemLogCsvWriter(IOptionsMonitor<UploadServiceSettings> uploadOptions)
    {
        _uploadOptions = uploadOptions;
    }

    public async Task<string> WriteRecordAsync(ItemLogRecord record, CancellationToken ct = default)
    {
        var folder = _uploadOptions.CurrentValue.CsvOutputFolder;
        Directory.CreateDirectory(folder);
        var fileName = $"record_{record.ID}_{record.DateTimeStamp:yyyyMMdd_HHmmss}.csv";
        var path = Path.Combine(folder, SanitizeFileName(fileName));

        var csv = ToCsv(record, includeHeader: false); // data only
        await File.WriteAllTextAsync(path, csv, Encoding.UTF8, ct);
        return path;
    }

    public async Task<string> WriteRecordsAsync(IEnumerable<ItemLogRecord> records, string? fileName = null, CancellationToken ct = default)
    {
        var list = records as IList<ItemLogRecord> ?? records.ToList();
        if (list.Count == 0)
            throw new InvalidOperationException("No records to write");

        var folder = _uploadOptions.CurrentValue.CsvOutputFolder;
        Directory.CreateDirectory(folder);
        var name = fileName ?? $"records_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        var path = Path.Combine(folder, SanitizeFileName(name));

        var sb = new StringBuilder();
        // No header line; write data rows only
        foreach (var r in list)
            sb.AppendLine(ToCsvLine(r));

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct);
        return path;
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
