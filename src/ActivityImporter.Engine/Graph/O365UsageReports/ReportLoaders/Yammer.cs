using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.UsageReports;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;

/// <summary>
/// https://docs.microsoft.com/en-us/graph/api/reportroot-getyammeractivityuserdetail?view=graph-rest-beta
/// </summary>
public class YammerUserUsageLoader : AbstractActivityLoader<YammerUserActivityLog, YammerUserActivityUserDetail>
{
    public YammerUserUsageLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger telemetry)
        : base(activityLoader, usageReportPersistence, telemetry)
    {
    }

    public override void PopulateReportSpecificMetadata(YammerUserActivityLog todaysLog, YammerUserActivityUserDetail userActivityReportPage)
    {
        // Convert serialised object to DB object
        todaysLog.ReadCount = GetOptionalInt(userActivityReportPage.ReadCount);
        todaysLog.LikedCount = GetOptionalInt(userActivityReportPage.LikedCount);
        todaysLog.PostedCount = GetOptionalInt(userActivityReportPage.PostedCount);
    }

    protected override long CountActivity(YammerUserActivityUserDetail activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;
        count += GetOptionalInt(activityPage.ReadCount);
        count += GetOptionalInt(activityPage.LikedCount);
        count += GetOptionalInt(activityPage.PostedCount);

        return count;
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getYammerActivityUserDetail";
    public override string DataContextPropertyName => nameof(DataContext.YammerUserActivityLogs);

}

/// <summary>
/// https://docs.microsoft.com/en-us/graph/api/reportroot-getyammerdeviceusageuserdetail?view=graph-rest-beta
/// </summary>
public class YammerDeviceUsageLoader : AbstractActivityLoader<YammerDeviceActivityLog, YammerDeviceActivityDetail>
{
    public YammerDeviceUsageLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger telemetry)
        : base(activityLoader, usageReportPersistence, telemetry)
    {
    }
    public override void PopulateReportSpecificMetadata(YammerDeviceActivityLog todaysLog, YammerDeviceActivityDetail userActivityReportPage)
    {
        // Convert serialised object to DB object
        todaysLog.UsedWeb = userActivityReportPage.UsedWeb;
        todaysLog.UsedIpad = userActivityReportPage.UsedIpad;
        todaysLog.UsedIphone = userActivityReportPage.UsedIphone;
        todaysLog.UsedAndroidPhone = userActivityReportPage.UsedAndroidPhone;
        todaysLog.UsedWindowsPhone = userActivityReportPage.UsedWindowsPhone;
        todaysLog.UsedOthers = userActivityReportPage.UsedOthers;
    }

    protected override long CountActivity(YammerDeviceActivityDetail activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;
        if (activityPage.UsedAndroidPhone.HasValue && activityPage.UsedAndroidPhone.Value) count++;
        if (activityPage.UsedWeb.HasValue && activityPage.UsedWeb.Value) count++;
        if (activityPage.UsedWindowsPhone.HasValue && activityPage.UsedWindowsPhone.Value) count++;
        if (activityPage.UsedIpad.HasValue && activityPage.UsedIpad.Value) count++;
        if (activityPage.UsedIphone.HasValue && activityPage.UsedIphone.Value) count++;
        if (activityPage.UsedOthers.HasValue && activityPage.UsedOthers.Value) count++;

        return count;
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getYammerDeviceUsageUserDetail";
    public override string DataContextPropertyName => nameof(DataContext.YammerDeviceActivityLogs);
}
