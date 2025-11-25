using TracesolCrossCheck_Upload_Service;
using TracesolCrossCheck_Upload_Service.Helpers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Serilog;
using CliWrap;
using CliWrap.Buffered;
using System.Security.Principal;

const string ServiceName = "TracesolCrossCheck_Upload_Service";
const string ServiceDisplayName = "Tracesol CrossCheck Upload Service";
const string ServiceDescription = "Tracesol CrossCheck Upload Service that monitors and uploads item log records.";
const string SettingsDirectory = "C:\\Users\\Public\\Documents\\TracesolCrossCheck";
const string SettingsFileName = "settings.json";

static bool IsAdministrator()
{
    try
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}

// Support simple install switch
if (args is { Length: 1 } && string.Equals(args[0], "/Install", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        if (!IsAdministrator())
        {
            Console.WriteLine("Installation requires an elevated (Administrator) command prompt.");
            return;
        }

        // Path to this executable
        string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Console.WriteLine("Unable to determine executable path.");
            return;
        }

        // Ensure settings directory exists and copy default settings
        var settingsDir = SettingsDirectory.Replace('\\', Path.DirectorySeparatorChar);
        var settingsPath = Path.Combine(settingsDir, SettingsFileName);
        Directory.CreateDirectory(settingsDir);

        var sourceAppSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            if (File.Exists(sourceAppSettings))
            {
                if (!File.Exists(settingsPath))
                {
                    File.Copy(sourceAppSettings, settingsPath);
                    Console.WriteLine($"Copied settings to {settingsPath}");
                }
                else
                {
                    Console.WriteLine($"Settings file already exists at {settingsPath}; leaving it unchanged.");
                }
            }
            else
            {
                Console.WriteLine($"Source appsettings.json not found at {sourceAppSettings}; no settings were copied.");
            }
        }
        catch (Exception copyEx)
        {
            Console.WriteLine($"Failed to copy settings: {copyEx}");
        }

        // sc.exe create <ServiceName> binPath= "<path>" start= auto type= own DisplayName= "..."
        await Cli.Wrap("sc")
            .WithArguments(new[]
            {
                "create", ServiceName,
                "binPath=", exePath,
                "start=", "auto",
                "type=", "own",
                "DisplayName=", ServiceDisplayName
            })
            .ExecuteAsync();

        // sc.exe description <ServiceName> "<description>"
        await Cli.Wrap("sc")
            .WithArguments(new[] { "description", ServiceName, ServiceDescription })
            .ExecuteAsync();

        // Verify registration
        var result = await Cli.Wrap("sc")
            .WithArguments(new[] { "query", ServiceName })
            .ExecuteBufferedAsync();

        Console.WriteLine(result.StandardOutput);
        Console.WriteLine($"Service '{ServiceName}' created with binPath=\"{exePath}\" and DisplayName=\"{ServiceDisplayName}\"\nDescription set to: {ServiceDescription}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
    }

    return;
}

// Support simple uninstall switch
if (args is { Length: 1 } && string.Equals(args[0], "/Uninstall", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        if (!IsAdministrator())
        {
            Console.WriteLine("Uninstall requires an elevated (Administrator) command prompt.");
            return;
        }

        // Try to stop the service (ignore errors if not running/not installed)
        try { await Cli.Wrap("sc").WithArguments(new[] { "stop", ServiceName }).ExecuteAsync(); } catch { }
        await Task.Delay(TimeSpan.FromSeconds(2));
        await Cli.Wrap("sc").WithArguments(new[] { "delete", ServiceName }).ExecuteAsync();
        Console.WriteLine($"Service '{ServiceName}' deleted.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to uninstall service: {ex}");
    }

    return;
}

// Ensure a minimal bootstrap logger before configuration is read
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Ensure service integration
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = ServiceName;
    });

    // Set content root to executable directory when running as a service
    var exeDir = AppContext.BaseDirectory;
    builder.Environment.ContentRootPath = exeDir;

    // Load settings from the fixed public-docs folder
    var settingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
        "TracesolCrossCheck"
    );
    var settingsPath = Path.Combine(settingsDir, SettingsFileName);

    // Throws if missing (by design, since you said it will always be there)
    builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);

    Log.Information("Config loaded. Settings path: {Path} Exists: {Exists}", settingsPath, File.Exists(settingsPath));

    // Ensure log directory exists
    var logDir = Path.Combine(settingsDir, "Upload_Logs");
    Directory.CreateDirectory(logDir);
    var logPath = Path.Combine(logDir, "upload.log");

    // Requested format with system tag: 2025-11-18 08:13:45 [WRN] [APP|DB|FILE] Message
    const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{System}] {Message:lj}{NewLine}{Exception}";

    // Configure Serilog as the logging provider (console + single static file)
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("System", "APP") // default category
        .WriteTo.Console(outputTemplate: outputTemplate)
        .WriteTo.File(
            path: logPath,
            outputTemplate: outputTemplate,
            rollOnFileSizeLimit: false,
            shared: true
        )
        .CreateLogger();

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    Log.Information("File logging configured at {LogPath}", logPath);

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
