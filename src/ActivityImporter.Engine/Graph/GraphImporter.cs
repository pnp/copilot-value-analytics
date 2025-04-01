using ActivityImporter.Engine.Graph.GraphUser;
using ActivityImporter.Engine.Graph.O365UsageReports;
using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;
using Common.DataUtils;
using Common.Engine.Config;
using Entities.DB.DbContexts;
using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph;

/// <summary>
/// Reads and saves all data read from Graph
/// </summary>
public class GraphImporter : AbstractApiLoader
{
    private readonly GraphAppIndentityOAuthContext _graphAppIndentityOAuthContext;
    private readonly AppConfig _appConfig;

    public GraphImporter(AppConfig appConfig, ILogger telemetry) : base(telemetry)
    {
        _graphAppIndentityOAuthContext = new GraphAppIndentityOAuthContext(telemetry, appConfig.ImportAuthConfig.ClientId, appConfig.ImportAuthConfig.TenantId, appConfig.ImportAuthConfig.ClientSecret, string.Empty, false);
        _appConfig = appConfig;
    }

    async Task InitAuth()
    {
        await _graphAppIndentityOAuthContext.InitClientCredential();
    }

    /// <summary>
    /// Main entry-point
    /// </summary>
    public async Task GetAndSaveAllGraphData()
    {
        await InitAuth();

        if (_graphAppIndentityOAuthContext.Creds == null)
        {
            throw new Exception("No Graph credentials found");
        }

        var httpClient = new ManualGraphCallClient(_graphAppIndentityOAuthContext, _telemetry);

        var userMetadaTimer = new JobTimer(_telemetry, "User metadata refresh");
        userMetadaTimer.Start();

        // Update Graph users first
        using (var userUpdater = new UserMetadataUpdater(_appConfig, _telemetry, _graphAppIndentityOAuthContext.Creds, httpClient))
        {
            await userUpdater.InsertAndUpdateDatabaseUsersFromGraph();
        }

        // Track finished event 
        userMetadaTimer.TrackFinishedEventAndStopTimer(AnalyticsEvent.FinishedSectionImport);

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_appConfig.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            var usageActivityTimer = new JobTimer(_telemetry, "Usage reports");
            usageActivityTimer.Start();

            // Global user activity report. Each thread creates own context.
            await GetAndSaveActivityReportsMultiThreaded(new GraphActivityLoader(httpClient, _telemetry));

            // Track finished event 
            usageActivityTimer.TrackFinishedEventAndStopTimer(AnalyticsEvent.FinishedSectionImport);
        }
    }

    public async Task GetAndSaveActivityReportsMultiThreaded(IUserActivityLoader loader)
    {
        _telemetry.LogInformation($"Reading all activity reports...");

        // Parallel-load all, each one with own DB context
        var importTasks = new List<Task>();

        var lookupIdCache = new ConcurrentLookupDbIdsCache();

        using var dbTeamsUserUsageLoader = GetDB();
        var sqlAdaptorTeamsUserUsageLoader = new SqlUsageReportPersistence(lookupIdCache, dbTeamsUserUsageLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbTeamsUserUsageLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new TeamsUserUsageLoader(loader, sqlAdaptorTeamsUserUsageLoader, _telemetry),
            "Teams user activity"));

        using var dbOutlookUserActivityLoader = GetDB();
        var sqlAdaptorOutlookUserActivityLoader = new SqlUsageReportPersistence(lookupIdCache, dbOutlookUserActivityLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbOutlookUserActivityLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new OutlookUserActivityLoader(loader, sqlAdaptorOutlookUserActivityLoader, _telemetry),
            "Outlook activity"));

        using var dbOneDriveUserActivityLoader = GetDB();
        var sqlAdaptorOneDriveUserActivityLoader = new SqlUsageReportPersistence(lookupIdCache, dbOneDriveUserActivityLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbOneDriveUserActivityLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new TeamsUserUsageLoader(loader, sqlAdaptorOneDriveUserActivityLoader, _telemetry),
            "OneDrive activity"));

        using var dbSharePointUserActivityLoader = GetDB();
        var sqlAdaptorSharePointUserActivityLoader = new SqlUsageReportPersistence(lookupIdCache, dbSharePointUserActivityLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbSharePointUserActivityLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new SharePointUserActivityLoader(loader, sqlAdaptorSharePointUserActivityLoader, _telemetry),
            "SharePoint user activity"));

        using var dbTeamsUserDeviceLoader = GetDB();
        var sqlAdaptorTeamsUserDeviceLoader = new SqlUsageReportPersistence(lookupIdCache, dbTeamsUserDeviceLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbTeamsUserDeviceLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new TeamsUserDeviceLoader(loader, sqlAdaptorTeamsUserDeviceLoader, _telemetry),
            "Teams user device activity"));

        using var dbAppPlatformUserActivityLoader = GetDB();
        var sqlAdaptorAppPlatformUserActivityLoader = new SqlUsageReportPersistence(lookupIdCache, dbAppPlatformUserActivityLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbAppPlatformUserActivityLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new AppPlatformUserActivityLoader(loader, sqlAdaptorAppPlatformUserActivityLoader, _telemetry),
            "App platform activity"));

        using var dbYammerUserUsageLoader = GetDB();
        var sqlAdaptorYammerUserUsageLoader = new SqlUsageReportPersistence(lookupIdCache, dbYammerUserUsageLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbYammerUserUsageLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new YammerUserUsageLoader(loader, sqlAdaptorYammerUserUsageLoader, _telemetry),
            "Yammer user activity"));

        using var dbYammerDeviceUsageLoader = GetDB();
        var sqlAdaptor = new SqlUsageReportPersistence(lookupIdCache, dbYammerDeviceUsageLoader, new Entities.DB.LookupCaches.Discrete.UserCache(dbYammerDeviceUsageLoader), _telemetry);
        importTasks.Add(LoadAndSaveReportAsync(new YammerDeviceUsageLoader(loader, sqlAdaptor, _telemetry),
            "Yammer device activity"));

        await Task.WhenAll(importTasks);

        _telemetry.LogInformation($"Activity reports imported.");
    }


    async Task<int> LoadAndSaveReportAsync<TReportDbType, TUserActivityUserDetail>(AbstractActivityLoader<TReportDbType, TUserActivityUserDetail> abstractActivityLoader,
        string thingWeAreImporting)
            where TReportDbType : AbstractUsageActivityLog, new()
            where TUserActivityUserDetail : AbstractActivityRecord
    {
        _telemetry.LogInformation($"Importing {thingWeAreImporting} reports...");

        var pagesSaved = await abstractActivityLoader.LoadAndSaveUsagePages();

        var total = pagesSaved.SelectMany(r => r.Value).Count();
        _telemetry.LogInformation($"Imported {total.ToString("N0")} {thingWeAreImporting} reports.");

        return total;
    }
    DataContext GetDB()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_appConfig.ConnectionStrings.SQL);

        return new DataContext(optionsBuilder.Options);
    }
}
