using ActivityImporter.Engine.ActivityAPI;
using ActivityImporter.Engine.ActivityAPI.Loaders;
using ActivityImporter.Engine.Graph;
using Common.Engine.Config;
using Entities.DB.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;

namespace ActivityImporter.ConsoleApp;

internal class ProgramTasks(AppConfig importConfig, ILogger logger)
{
    /// <summary>
    /// Graph data
    /// </summary>
    internal async Task GetGraphTeamsAndUserData()
    {
        logger.LogInformation("Starting Teams & Graph import.");
        var graphReader = new GraphImporter(importConfig, logger);

        try
        {
            await graphReader.GetAndSaveAllGraphData();
        }
        catch (ODataError ex)
        {
            // Don't make a drama if Graph permissions aren't assigned yet.
            if (ex.ResponseStatusCode == 403)
            {
                logger.LogWarning("ERROR: Can't access Teams user data - are application permissions configured correctly?");
                return;
            }
            else
            {
                logger.LogError(ex.ToString());

                throw;
            }
        }

        logger.LogInformation("Finished Graph API import tasks.");
    }

    /// <summary>
    /// Activity API
    /// </summary>
    internal async Task DownloadAndSaveActivityData()
    {
        // Remember start time
        var startTime = DateTime.Now;

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(importConfig.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            var spFilterList = await SharePointOrgUrlsFilterConfig.Load(db);

            if (spFilterList.OrgUrlConfigs.Count == 0)
            {
                logger.LogCritical("FATAL ERROR: No import URLs found in database! " +
                    "This means everything would be ignored for SharePoint audit data. " +
                    "Add at least one URL to the import_url_filter table audit import to work.");

                return;
            }

            logger.LogInformation("\nBeginning import. Filtering for SharePoint events below these URLs:");

            // Print URLs
            spFilterList.Print(logger);
            Console.WriteLine();

            logger.LogInformation($"\nStarting activity import for {spFilterList.OrgUrlConfigs.Count} url filters");

            // Start new O365 activity download session
            const int MAX_IMPORTS_PER_BATCH = 100000;
            var importer = new ActivityWebImporter(importConfig.ImportAuthConfig, logger, MAX_IMPORTS_PER_BATCH);

            var sqlAdaptor = new ActivityReportSqlPersistenceManager(db, importConfig, spFilterList, logger);
            try
            {
                var stats = await importer.LoadReportsAndSave(sqlAdaptor);

                // Output stats
                logger.LogInformation($"Finished activity import. Time taken in = {DateTime.Now.Subtract(startTime).TotalMinutes.ToString("N2")} minutes. Stats: {stats}", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError($"Got unexpected exception importing user usage activity: {ex.Message}");
            }
        }
    }
}
