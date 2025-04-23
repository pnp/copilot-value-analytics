using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.UsageReports;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;

public class OutlookUserActivityLoader : AbstractActivityLoader<OutlookUsageActivityLog, OutlookUserActivityUserRecord>
{
    public OutlookUserActivityLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger logger)
        : base(activityLoader, usageReportPersistence, logger)
    {
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getEmailActivityUserDetail";

    public override void PopulateReportSpecificMetadata(OutlookUsageActivityLog todaysLog, OutlookUserActivityUserRecord userActivityReportPage)
    {
        todaysLog.MeetingsCreated = userActivityReportPage.MeetingCreated;
        todaysLog.ReadCount = userActivityReportPage.ReadCount;
        todaysLog.ReceiveCount = userActivityReportPage.ReceiveCount;
        todaysLog.SendCount = userActivityReportPage.SendCount;
        todaysLog.MeetingsInteracted = userActivityReportPage.MeetingInteracted;

    }

    protected override long CountActivity(OutlookUserActivityUserRecord activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;
        count += activityPage.ReadCount;
        count += activityPage.ReceiveCount;
        count += activityPage.SendCount;
        count += activityPage.MeetingCreated;
        count += activityPage.MeetingInteracted;

        return count;
    }
    public override string DataContextPropertyName => nameof(DataContext.OutlookUsageActivityLogs);

}
