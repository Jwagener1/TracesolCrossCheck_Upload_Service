using TracesolCrossCheck_Upload_Service;
using Microsoft.Extensions.Options;                // NEW
using Microsoft.Extensions.Configuration;          // NEW

var builder = Host.CreateApplicationBuilder(args);


// Load settings from the fixed public-docs folder
var settingsDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
    "TracesolCrossCheck"
);
var settingsPath = Path.Combine(settingsDir, "settings.json");

// Throws if missing (by design, since you said it will always be there)
builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);

// Bind just the UploadService section to our POCO
builder.Services.Configure<UploadServiceSettings>(
    builder.Configuration.GetSection("UploadService")
);

// Bind database settings
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database")
);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
