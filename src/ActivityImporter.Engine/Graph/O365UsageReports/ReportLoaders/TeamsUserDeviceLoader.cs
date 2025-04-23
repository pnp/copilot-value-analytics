using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.UsageReports;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;


/// <summary>
/// https://docs.microsoft.com/en-us/graph/api/reportroot-getteamsdeviceusageuserdetail?view=graph-rest-beta
/// </summary>
public class TeamsUserDeviceLoader : AbstractActivityLoader<GlobalTeamsUserDeviceUsageLog, TeamsDeviceUsageUserDetail>
{
    public TeamsUserDeviceLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger telemetry)
        : base(activityLoader, usageReportPersistence, telemetry)
    {
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getTeamsDeviceUsageUserDetail";

    public override string DataContextPropertyName => nameof(DataContext.TeamsUserDeviceUsageLog);

    public override void PopulateReportSpecificMetadata(GlobalTeamsUserDeviceUsageLog dateRequestedLog, TeamsDeviceUsageUserDetail reportPage)
    {
        dateRequestedLog.UsedAndroidPhone = reportPage.UsedAndroidPhone;
        dateRequestedLog.UsedIOS = reportPage.UsediOS;
        dateRequestedLog.UsedMac = reportPage.UsedMac;
        dateRequestedLog.UsedWeb = reportPage.UsedWeb;
        dateRequestedLog.UsedWindows = reportPage.UsedWindows;
        dateRequestedLog.UsedWindowsPhone = reportPage.UsedWindowsPhone;
        dateRequestedLog.UsedLinux = reportPage.UsedLinux;
        dateRequestedLog.UsedChromeOS = reportPage.UsedChromeOs;
    }

    protected override long CountActivity(TeamsDeviceUsageUserDetail activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;
        if (activityPage.UsedAndroidPhone.HasValue && activityPage.UsedAndroidPhone.Value) count++;
        if (activityPage.UsediOS.HasValue && activityPage.UsediOS.Value) count++;
        if (activityPage.UsedMac.HasValue && activityPage.UsedMac.Value) count++;
        if (activityPage.UsedWeb.HasValue && activityPage.UsedWeb.Value) count++;
        if (activityPage.UsedWindows.HasValue && activityPage.UsedWindows.Value) count++;
        if (activityPage.UsedWindowsPhone.HasValue && activityPage.UsedWindowsPhone.Value) count++;
        if (activityPage.UsedLinux.HasValue && activityPage.UsedLinux.Value) count++;
        if (activityPage.UsedChromeOs.HasValue && activityPage.UsedChromeOs.Value) count++;

        return count;
    }
}