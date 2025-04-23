using ActivityImporter.ConsoleApp;
using Common.DataUtils.Config;
using Common.Engine.Config;
using Entities.DB;
using Entities.DB.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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


var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
}).CreateLogger("Import console");


Console.WriteLine("Starting import job");

var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
optionsBuilder.UseSqlServer(config.ConnectionStrings.SQL);

using (var db = new DataContext(optionsBuilder.Options))
{
    await DbInitialiser.EnsureInitialised(db, logger, config.TestUPN);

    if (config.DevMode)
    {
        await FakeDataGen.GenerateFakeActivityForAllUsers(db, logger);
    }

    // Import things
    var t = new ProgramTasks(config, logger);
    await t.DownloadAndSaveActivityData();

    await t.GetGraphTeamsAndUserData();
}
