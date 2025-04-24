using Entities.DB.DbContexts;
using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Entities.DB;

public class FakeDataGen
{
    public static async Task GenerateFakeActivityForAllUsers(DataContext context, ILogger logger)
    {
        logger.LogInformation("Generating fake activity for all users");
        foreach (var user in await context.Users.ToListAsync())
        {
            await FakeDataGen.GenerateFakeCopilotFor(user.UserPrincipalName, context, logger);
            await FakeDataGen.GenerateFakeOfficeActivityFor(user.UserPrincipalName, DateTime.Now, context, logger);
            await context.SaveChangesAsync();
        }
        logger.LogInformation("Finished generating fake activity for all users");
    }
    public static async Task GenerateFakeCopilotFor(string forUpn, DataContext context, ILogger logger)
    {
        var editDoc = context.CopilotActivities.FirstOrDefault(a => a.Name == DbInitialiser.ACTIVITY_NAME_EDIT_DOC)!;
        var getHighlights = context.CopilotActivities.FirstOrDefault(a => a.Name == DbInitialiser.ACTIVITY_NAME_GET_HIGHLIGHTS)!;

        var user = context.Users.FirstOrDefault(u => u.UserPrincipalName == forUpn);
        if (user == null)
        {
            logger.LogWarning($"Creating new user with UPN {forUpn}");
            user = new User { UserPrincipalName = forUpn };
        }

        // Add a meeting event with cop
        context.Add(new CopilotEventMetadataMeeting
        {
            RelatedChat = new CopilotChat
            {
                AppHost = "Teams",
                AuditEvent = new CommonAuditEvent
                {
                    User = user,
                    Id = Guid.NewGuid(),
                    TimeStamp = DateTime.Now.AddDays(-1),
                    Operation = new EventOperation { Name = "Meeting operation " + DateTime.Now.Ticks }
                }
            },
            OnlineMeeting = new OnlineMeeting { Name = "Weekly Team Sync - " + Enum.GetName(DateTime.Now.DayOfWeek), MeetingId = "Join Link" }
        });

        var fileName = $"Test File {DateTime.Now.Ticks}.docx";
        context.Add(new CopilotEventMetadataFile
        {

            RelatedChat = new CopilotChat
            {
                AppHost = "Office",
                AuditEvent = new CommonAuditEvent
                {
                    User = user,
                    Id = Guid.NewGuid(),
                    TimeStamp = DateTime.Now.AddDays(-2),
                    Operation = new EventOperation { Name = "File operation " + DateTime.Now.Ticks }
                }
            },
            FileName = new SPEventFileName { Name = fileName },
            FileExtension = await context.SharePointFileExtensions.SingleOrDefaultAsync(e => e.Name == "docx") ?? new SPEventFileExtension { Name = "docx" },
            Url = new Entities.SP.Url { FullUrl = $"https://devbox.sharepoint.com/Docs/{fileName}" },
            Site = context.Sites.FirstOrDefault() ?? new Entities.SP.Site { UrlBase = "https://copilot.sharepoint.com" },
        });

        // Add an agent chat event
        var randomAgent = await context.CopilotAgentTypes
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        context.Add(new CopilotChat
        {
            AppHost = "Teams",
            AuditEvent = new CommonAuditEvent
            {
                User = user,
                Id = Guid.NewGuid(),
                TimeStamp = DateTime.Now.AddDays(-3),
                Operation = new EventOperation { Name = "Agent operation " + DateTime.Now.Ticks }
            },
            Agent = randomAgent
        });
    }

    public static async Task GenerateFakeOfficeActivityFor(string forUpn, DateTime forWhen, DataContext context, ILogger logger)
    {

        var user = context.Users.FirstOrDefault(u => u.UserPrincipalName == forUpn);
        if (user == null)
        {
            logger.LogWarning($"Creating new user with UPN {forUpn}");
            user = new User { UserPrincipalName = forUpn };
        }

        // Get a random SharePoint event type or create a new one
        var randomSPEventType = await context.SharePointEventType
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSPEventType == null)
            randomSPEventType = new SPEventType { Name = "Test" + DateTime.Now.Ticks };

        var randomSPFileExtension = await context.SharePointFileExtensions
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSPFileExtension == null)
            randomSPFileExtension = new SPEventFileExtension { Name = "Test" + DateTime.Now.Ticks };
        var randomSPEventFileName = await context.SharePointFileNames
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSPEventFileName == null)
            randomSPEventFileName = new SPEventFileName { Name = "Test" + DateTime.Now.Ticks };
        var randomSPEventUrl = await context.Urls.OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSPEventUrl == null)
            randomSPEventUrl = new Entities.SP.Url { FullUrl = "https://devbox.sharepoint.com/Docs/TestFile" + DateTime.Now.Ticks + ".docx" };
        var randomSite = await context.Sites.OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSite == null)
            randomSite = new Entities.SP.Site { UrlBase = "https://devbox.sharepoint.com/" };
        var randomSPEventOperation = await context.EventOperations
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomSPEventOperation == null)
            randomSPEventOperation = new EventOperation { Name = "Test" + DateTime.Now.Ticks };

        var randomUrl = await context.Urls
            .OrderBy(e => Guid.NewGuid())
            .FirstOrDefaultAsync();
        if (randomUrl == null)
            randomUrl = new Entities.SP.Url { FullUrl = "https://devbox.sharepoint.com/Docs/TestFile" + DateTime.Now.Ticks + ".docx" };


        var rnd = new Random();
        context.SharePointEvents.Add(new SharePointEventMetadata
        {
            AuditEvent = new CommonAuditEvent
            {
                User = user,
                Id = Guid.NewGuid(),
                TimeStamp = forWhen,
                Operation = new EventOperation { Name = "Operation " + DateTime.Now.Ticks }
            },
            ItemType = randomSPEventType,
            FileName = randomSPEventFileName,
            Url = randomUrl,
            RelatedSite = randomSite,
            FileExtension = randomSPFileExtension,
        });

        context.OutlookUsageActivityLogs.Add(new Entities.UsageReports.OutlookUsageActivityLog
        {
            User = user,
            DateOfActivity = DateTime.Now,
            MeetingsCreated = rnd.Next(0, 1),
            MeetingsInteracted = rnd.Next(0, 1),
            ReadCount = rnd.Next(0, 1),
            SendCount = rnd.Next(0, 1),
        });

        context.AppPlatformUserUsageLog.Add(new Entities.UsageReports.AppPlatformUserActivityLog
        {
            User = user,
            DateOfActivity = DateTime.Now,
            Excel = true,
            ExcelMobile = true,
            ExcelWindows = true,
            ExcelMac = true,
            ExcelWeb = true,
            Outlook = true,
            OutlookMac = true,
            OutlookWindows = true,
            OutlookWeb = true,
            PowerPoint = true,
            PowerPointMobile = true,
            PowerPointWindows = true,
            PowerPointMac = true,
            PowerPointWeb = true,
            Teams = true,
            TeamsMobile = true,
            TeamsWindows = true,
            TeamsMac = true,
            TeamsWeb = true,
            Word = true,
            WordMobile = true,
            WordWindows = true,
            WordMac = true,
            WordWeb = true,
            OneNote = true,
            OneNoteMobile = true,
            OneNoteWindows = true,
            OneNoteMac = true,
            OneNoteWeb = true,
            Mac = true,
            Mobile = true,
            Web = true,
            Windows = true,
            OutlookMobile = true,
        });

        context.SharePointUserActivityLogs.Add(new Entities.UsageReports.SharePointUserActivityLog
        {
            DateOfActivity = DateTime.Now,
            User = user,

            SharedExternally = rnd.Next(0, 1),
            SharedInternally = rnd.Next(0, 1),
            Synced = rnd.Next(0, 1),
            ViewedOrEdited = rnd.Next(0, 1),
        });

        context.OneDriveUserActivityLogs.Add(new Entities.UsageReports.OneDriveUserActivityLog
        {
            DateOfActivity = DateTime.Now,
            User = user,
            SharedExternally = rnd.Next(0, 1),
            SharedInternally = rnd.Next(0, 1),
            Synced = rnd.Next(0, 1),
            ViewedOrEdited = rnd.Next(0, 1),
        });

        context.YammerUserActivityLogs.Add(new Entities.UsageReports.YammerUserActivityLog
        {
            DateOfActivity = DateTime.Now,
            User = user,
            LikedCount = rnd.Next(0, 1),
            PostedCount = rnd.Next(0, 1),
            ReadCount = rnd.Next(0, 1),
        });

        context.YammerDeviceActivityLogs.Add(new Entities.UsageReports.YammerDeviceActivityLog
        {
            DateOfActivity = DateTime.Now,
            User = user,
            UsedAndroidPhone = true,
            UsedIpad = true,
            UsedIphone = true,
            UsedOthers = true,
            UsedWindowsPhone = true,
            UsedWeb = true,
        });

        context.TeamUserActivityLogs.Add(new Entities.UsageReports.GlobalTeamsUserUsageLog
        {
            DateOfActivity = DateTime.Now,
            User = user,
            AdHocMeetingsAttendedCount = rnd.Next(0, 1),
            AdHocMeetingsOrganizedCount = rnd.Next(0, 1),
            CallCount = rnd.Next(0, 1),
            MeetingCount = rnd.Next(0, 1),
            MeetingsAttendedCount = rnd.Next(0, 1),
            MeetingsOrganizedCount = rnd.Next(0, 1),
            PrivateChatMessageCount = rnd.Next(0, 1),
            TeamChatMessageCount = rnd.Next(0, 1),
            PostMessages = rnd.Next(0, 1),
            ReplyMessages = rnd.Next(0, 1),
            ScheduledOneTimeMeetingsAttendedCount = rnd.Next(0, 1),
            ScheduledOneTimeMeetingsOrganizedCount = rnd.Next(0, 1),
            ScheduledRecurringMeetingsAttendedCount = rnd.Next(0, 1),
            ScheduledRecurringMeetingsOrganizedCount = rnd.Next(0, 1),
            AudioDurationSeconds = rnd.Next(0, 1),
            VideoDurationSeconds = rnd.Next(0, 1),
            ScreenShareDurationSeconds = rnd.Next(0, 1),
            UrgentMessages = rnd.Next(0, 1),
        });

        context.TeamsUserDeviceUsageLog.Add(new Entities.UsageReports.GlobalTeamsUserDeviceUsageLog
        {
            DateOfActivity = DateTime.Now,
            User = user,
            UsedAndroidPhone = rnd.Next(0, 1) == 1 ? true : false,
            UsedChromeOS = rnd.Next(0, 1) == 1 ? true : false,
            UsedIOS = rnd.Next(0, 1) == 1 ? true : false,
            UsedLinux = rnd.Next(0, 1) == 1 ? true : false,
            UsedMac = rnd.Next(0, 1) == 1 ? true : false,
            UsedWindows = rnd.Next(0, 1) == 1 ? true : false,
            UsedWindowsPhone = rnd.Next(0, 1) == 1 ? true : false,
            UsedWeb = rnd.Next(0, 1) == 1 ? true : false,
        });
    }
}
