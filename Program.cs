using TracesolCrossCheck_Upload_Service;
using Microsoft.Extensions.Options;                // NEW
using Microsoft.Extensions.Configuration;          // NEW
using Serilog;
using TracesolCrossCheck_Upload_Service.Helpers;

var builder = Host.CreateApplicationBuilder(args);

// Load settings from the fixed public-docs folder
var settingsDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
    "TracesolCrossCheck"
);
var settingsPath = Path.Combine(settingsDir, "settings.json");

// Throws if missing (by design, since you said it will always be there)
builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);

// Ensure log directory exists
var logDir = Path.Combine(settingsDir, "Upload_Logs");
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, "upload.log");

// Requested format with system tag: 2025-11-18 08:13:45 [WRN] [APP|DB|FILE] Message
const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{System}] {Message:lj}{NewLine}{Exception}";

// Configure Serilog as the logging provider (console + single static file)
// We do not use rolling here to avoid date in filename; a background service truncates at midnight.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("System", "APP") // default category
    .WriteTo.Console(outputTemplate: outputTemplate)
    .WriteTo.File(
        path: logPath,
        outputTemplate: outputTemplate,
        // No rolling interval => single file named upload.log
        rollOnFileSizeLimit: false,
        shared: true
    )
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Bind just the Upload section to our POCO
builder.Services.Configure<UploadServiceSettings>(
    builder.Configuration.GetSection("Upload")
);

// Bind database settings
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database")
);

// Register helpers
builder.Services.AddSingleton<IDbConnectionHelper, DbConnectionHelper>();
builder.Services.AddSingleton<IItemLogQueryHelper, ItemLogQueryHelper>();
builder.Services.AddSingleton<IItemLogCsvWriter, ItemLogCsvWriter>();
builder.Services.AddSingleton<IItemLogUpdateHelper, ItemLogUpdateHelper>();

// Background service to truncate upload.log at midnight so the file resets daily
builder.Services.AddHostedService<LogMaintenanceService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
