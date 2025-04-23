using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.UsageReports;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;

// https://docs.microsoft.com/en-us/graph/api/reportroot-getonedriveactivityuserdetail?view=graph-rest-beta
public class OneDriveUserActivityLoader : AbstractActivityLoader<OneDriveUserActivityLog, OneDriveUserActivityRecord>
{
    public OneDriveUserActivityLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger logger)
        : base(activityLoader, usageReportPersistence, logger)
    {
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getOneDriveActivityUserDetail";
    public override void PopulateReportSpecificMetadata(OneDriveUserActivityLog todaysLog, OneDriveUserActivityRecord userActivityReportPage)
    {
        todaysLog.SharedInternally = userActivityReportPage.SharedInternally;
        todaysLog.SharedExternally = userActivityReportPage.SharedExternally;
        todaysLog.Synced = userActivityReportPage.Synced;
        todaysLog.ViewedOrEdited = userActivityReportPage.ViewedOrEdited;
    }

    protected override long CountActivity(OneDriveUserActivityRecord activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;

        count += activityPage.SharedInternally;
        count += activityPage.SharedExternally;
        count += activityPage.Synced;
        count += activityPage.ViewedOrEdited;

        return count;
    }
    public override string DataContextPropertyName => nameof(DataContext.OneDriveUserActivityLogs);
}
