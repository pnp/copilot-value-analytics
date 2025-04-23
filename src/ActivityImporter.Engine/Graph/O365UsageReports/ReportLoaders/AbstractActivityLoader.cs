using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Common.DataUtils;
using Entities.DB.Entities;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;


/// <summary>
/// Generic usage report loader. Recursively loads and saves any Graph activity report.
/// </summary>
/// <typeparam name="TReportDbType">Type of EF table</typeparam>
/// <typeparam name="TPagableResponse">Type of report that's a pageable response</typeparam>
/// <typeparam name="TAbstractActivityRecord">Type of report page</typeparam>
public abstract class AbstractActivityLoader<TReportDbType, TAbstractActivityRecord>
    where TReportDbType : AbstractUsageActivityLog, new()
    where TAbstractActivityRecord : AbstractActivityRecord
{
    private readonly IUserActivityLoader _graphActivityLoader;
    private readonly IUsageReportPersistence _usageReportPersistence;
    private readonly ILogger _telemetry;

    internal AbstractActivityLoader(IUserActivityLoader graphActivityLoader, IUsageReportPersistence usageReportPersistence, ILogger telemetry)
    {
        _graphActivityLoader = graphActivityLoader;
        _usageReportPersistence = usageReportPersistence;
        _telemetry = telemetry;
    }

    public const int MAX_DAYS_BACK = 28;
    public abstract string ReportGraphURL { get; }

    public async Task<Dictionary<DateTime, List<TAbstractActivityRecord>>> LoadAndSaveUsagePages()
    {
        var pages = new Dictionary<DateTime, List<TAbstractActivityRecord>>();

        var daysBackMax = MAX_DAYS_BACK;       // Docs say 30, but in reality it's 28 https://learn.microsoft.com/en-us/graph/api/reportroot-getm365appuserdetail?view=graph-rest-1.0&tabs=http#function-parameters
        var newestActivityDate = await _usageReportPersistence.GetNewestActivityDateForAllUsers(this);
        if (newestActivityDate != null)
        {
            const int DAYS_BACK_PADDING = 1;
            daysBackMax = (DateTime.Now - newestActivityDate.Value).Days + DAYS_BACK_PADDING;
            if (daysBackMax > MAX_DAYS_BACK)
            {
                daysBackMax = MAX_DAYS_BACK;
            }
            _telemetry.LogInformation($"Last activity for all users for {this.DataContextPropertyName}: {newestActivityDate.Value.ToGraphDateString()}. Reading {daysBackMax} days back");
        }
        else
        {
            _telemetry.LogWarning($"No activity found for {this.DataContextPropertyName}. Defaulting to {daysBackMax} days back to read");
        }

        for (int daysBackIdx = 0; daysBackIdx < daysBackMax - 1; daysBackIdx++)
        {
            var daysBack = (daysBackIdx + 1) * -1;      // Don't ask for too many days back. It's 28 days max, including today.
            var dt = DateTime.Now.AddDays(daysBack);

            _telemetry.LogDebug($"Loading {GetType().Name} for date {dt.ToGraphDateString()}");

            var dayReports = await _graphActivityLoader.LoadReport<TAbstractActivityRecord>(dt, ReportGraphURL);

            if (dayReports.Count > 0 && dayReports[0] is AbstractUserActivityUserRecord)
            {
                // Check if UPN is anonymous
                foreach (var reportPage in dayReports)
                {
                    var reportPageRecord = reportPage as AbstractUserActivityUserRecord;
                    if (reportPageRecord != null)
                    {
                        // If this page is about a user activity (as opposed to a group activity)

                        // Check if UPN is anonymous due to Office 365 settings
                        if (!CommonStringUtils.IsEmail(reportPageRecord.UPNFieldVal))
                        {
                            _telemetry.LogCritical($"Config Error: Usage reports have associated user email concealed - " +
                                                        $"we won't be able to link any activity back to users. " +
                                                        $"Please refer to the prerequisites documentation.");

                            // Don't save data
                            return new Dictionary<DateTime, List<TAbstractActivityRecord>>();
                        }

                        // Let's see if we should load more data for this user
                        var dtLastActivityDate = CommonStringUtils.FromGraphDateString(reportPage.LastActivityDateString);
                    }
                }
            }
            pages.Add(dt, dayReports);
        }

        _telemetry.LogInformation($"Saving {GetType().Name} reports to DB");
        await _usageReportPersistence.SaveLoadedReports(pages, this);

        return pages;
    }

    protected abstract long CountActivity(TAbstractActivityRecord activityPage);

    /// <summary>
    /// Usually the name of the property table in the EF context
    /// </summary>
    public abstract string DataContextPropertyName { get; }

    public abstract void PopulateReportSpecificMetadata(TReportDbType newRecord, TAbstractActivityRecord activityPage);

    protected int GetOptionalInt(int? i) => i ?? 0;
}
