using ActivityImporter.Engine.Graph.O365UsageReports.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.UsageReports;
using Microsoft.Extensions.Logging;

namespace ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;

public class TeamsUserUsageLoader : AbstractActivityLoader<GlobalTeamsUserUsageLog, TeamsUserActivityUserRecord>
{
    public TeamsUserUsageLoader(IUserActivityLoader activityLoader, IUsageReportPersistence usageReportPersistence, ILogger logger)
        : base(activityLoader, usageReportPersistence, logger)
    {
    }

    public override string ReportGraphURL => "https://graph.microsoft.com/beta/reports/getTeamsUserActivityUserDetail";


    public override void PopulateReportSpecificMetadata(GlobalTeamsUserUsageLog todaysLog, TeamsUserActivityUserRecord userActivityReportPage)
    {
        // Convert serialised object to DB object
        todaysLog.CallCount = userActivityReportPage.CallCount;
        todaysLog.MeetingCount = userActivityReportPage.MeetingCount;
        todaysLog.PrivateChatMessageCount = userActivityReportPage.PrivateChatMessageCount;
        todaysLog.TeamChatMessageCount = userActivityReportPage.TeamChatMessageCount;

        todaysLog.AdHocMeetingsAttendedCount = userActivityReportPage.AdHocMeetingsAttendedCount;
        todaysLog.AdHocMeetingsOrganizedCount = userActivityReportPage.AdHocMeetingsOrganizedCount;
        todaysLog.MeetingsAttendedCount = userActivityReportPage.MeetingsAttendedCount;
        todaysLog.MeetingsOrganizedCount = userActivityReportPage.MeetingsOrganizedCount;
        todaysLog.ScheduledOneTimeMeetingsAttendedCount = userActivityReportPage.ScheduledOneTimeMeetingsAttendedCount;
        todaysLog.ScheduledOneTimeMeetingsOrganizedCount = userActivityReportPage.ScheduledOneTimeMeetingsOrganizedCount;
        todaysLog.ScheduledRecurringMeetingsAttendedCount = userActivityReportPage.ScheduledRecurringMeetingsAttendedCount;
        todaysLog.ScheduledRecurringMeetingsOrganizedCount = userActivityReportPage.ScheduledRecurringMeetingsOrganizedCount;


        todaysLog.UrgentMessages = userActivityReportPage.UrgentMessages;
        todaysLog.PostMessages = userActivityReportPage.PostMessages;
        todaysLog.ReplyMessages = userActivityReportPage.ReplyMessages;

        // ISO8601 duration strings.
        todaysLog.AudioDurationSeconds = System.Xml.XmlConvert.ToTimeSpan(userActivityReportPage.AudioDuration).Seconds;
        todaysLog.VideoDurationSeconds = System.Xml.XmlConvert.ToTimeSpan(userActivityReportPage.VideoDuration).Seconds;
        todaysLog.ScreenShareDurationSeconds = System.Xml.XmlConvert.ToTimeSpan(userActivityReportPage.ScreenShareDuration).Seconds;
    }

    protected override long CountActivity(TeamsUserActivityUserRecord activityPage)
    {
        if (activityPage is null)
        {
            throw new ArgumentNullException(nameof(activityPage));
        }

        long count = 0;
        count += activityPage.AdHocMeetingsAttendedCount;
        count += activityPage.AdHocMeetingsOrganizedCount;
        count += activityPage.CallCount;
        count += activityPage.MeetingCount;
        count += activityPage.MeetingsAttendedCount;
        count += activityPage.MeetingsOrganizedCount;
        count += activityPage.PrivateChatMessageCount;
        count += activityPage.ScheduledOneTimeMeetingsAttendedCount;
        count += activityPage.ScheduledOneTimeMeetingsOrganizedCount;
        count += activityPage.ScheduledRecurringMeetingsAttendedCount;
        count += activityPage.ScheduledRecurringMeetingsOrganizedCount;
        count += activityPage.TeamChatMessageCount;

        return count;
    }
    public override string DataContextPropertyName => nameof(DataContext.TeamUserActivityLogs);

}
