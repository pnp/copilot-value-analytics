using Entities.DB;
using Entities.DB.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UnitTests;

public abstract class AbstractTest
{
    protected ILogger _logger;
    protected DataContext _db;
    protected TestsConfig _config;

    public AbstractTest()
    {
        _logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Tests");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
            .AddJsonFile("appsettings.json", true);

        var configCollection = builder.Build();
        _config = new TestsConfig(configCollection);

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_config.ConnectionStrings.SQL);

        _db = new DataContext(optionsBuilder.Options);
    }


    [TestInitialize]
    public async Task TestInitialize()
    {
        await DbInitialiser.EnsureInitialised(_db, _logger, _config.TestUPN, true);
    }

    protected ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger<T>();
    }
}
