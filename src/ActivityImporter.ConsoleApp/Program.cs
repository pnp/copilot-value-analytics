using ActivityImporter.ConsoleApp;
using Common.DataUtils.Config;
using Common.Engine.Config;
using Entities.DB;
using Entities.DB.DbContexts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights.Channel;
using Common.Engine;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
    .AddJsonFile("appsettings.json", true);

var configCollection = builder.Build();

AppConfig? config = null;
try
{
    config = new AppConfig(configCollection);
}
catch (ConfigurationMissingException ex)
{
    // Show what the required config structure looks like
    config = new AppConfig();
    Console.WriteLine($"FATAL: Invalid config found - {ex.Message}. Required config structure (with values):");
    Console.WriteLine(JsonConvert.SerializeObject(config, Formatting.Indented));
    throw;
}

using var channel = new InMemoryChannel();
try
{
    var services = new ServiceCollection();
    services.Configure<TelemetryConfiguration>(tConfig => tConfig.TelemetryChannel = channel);
    services.AddLogging(builder =>
    {
        builder.AddConsole();

        if (config.AppInsightsConnectionString != null)
        {
            builder.AddApplicationInsights(
                configureTelemetryConfiguration: (c) => c.ConnectionString = config.AppInsightsConnectionString,
                configureApplicationInsightsLoggerOptions: (options) => { }
            );
        }
        else
        {
            Console.WriteLine("No Application Insights connection string found. " +
                "Telemetry will not be sent to Application Insights.");
        }
    });

    var serviceProvider = services.BuildServiceProvider();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation($"Importer {VersionConstants.Version} starting import job");

    var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
    optionsBuilder.UseSqlServer(config.ConnectionStrings.SQL);

    using (var db = new DataContext(optionsBuilder.Options))
    {
        await DbInitialiser.EnsureInitialised(db, logger, config.TestUPN, config.DevMode);

        if (config.DevMode)
        {
            await FakeDataGen.GenerateFakeActivityForAllUsers(db, logger);
        }

        // Import things
        var t = new ProgramTasks(config, logger);
        await t.DownloadAndSaveActivityData();

        await t.GetGraphTeamsAndUserData();
    }
}
finally
{
    // Explicitly call Flush() followed by Delay, as required in console apps.
    // This ensures that even if the application terminates, telemetry is sent to the back end.
    channel.Flush();

    await Task.Delay(TimeSpan.FromMilliseconds(1000));
}
