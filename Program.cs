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

// Configure Serilog as the logging provider (console sink + config)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Bind just the UploadService section to our POCO
builder.Services.Configure<UploadServiceSettings>(
    builder.Configuration.GetSection("UploadService")
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

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
