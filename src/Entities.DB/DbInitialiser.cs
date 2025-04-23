using Common.DataUtils;
using Entities.DB.DbContexts;
using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Entities.DB;

public class DbInitialiser
{
    public const string ACTIVITY_NAME_EDIT_DOC = "Edit Document";
    public const string ACTIVITY_NAME_GET_HIGHLIGHTS = "Get highlights";
    /// <summary>
    /// Ensure created and with base data
    /// </summary>
    public static async Task EnsureInitialised(DataContext context, ILogger logger, string? defaultUserUPN)
    {
        var createdNewDb = await context.Database.EnsureCreatedAsync();

        if (createdNewDb)
        {
            logger.LogInformation("Database created");
            if (defaultUserUPN != null)
            {
                logger.LogInformation("Creating default user");
                var defaultUser = new User
                {
                    UserPrincipalName = defaultUserUPN
                };
                context.Users.Add(defaultUser);
                await context.SaveChangesAsync();

                // Add base lookup data
                logger.LogInformation("Adding base lookup data");

                // Add some base activity types
                var activityTypeDoc = new CopilotActivityType { Name = CopilotActivityType.Document };
                var activityTypeMeeting = new CopilotActivityType { Name = CopilotActivityType.Meeting };
                var activityTypeEmail = new CopilotActivityType { Name = CopilotActivityType.Email };
                var activityTypeChat = new CopilotActivityType { Name = CopilotActivityType.Chat };
                var activityTypeOther = new CopilotActivityType { Name = CopilotActivityType.Other };
                context.CopilotActivityTypes.AddRange([activityTypeChat, activityTypeDoc, activityTypeEmail, activityTypeMeeting, activityTypeOther]);

                // Add some base activities
                context.CopilotActivities.Add(new CopilotActivity { Name = "Draft email", ActivityType = activityTypeEmail });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Summarise email", ActivityType = activityTypeEmail });

                var editDoc = new CopilotActivity { Name = ACTIVITY_NAME_EDIT_DOC, ActivityType = activityTypeDoc };       // Need this later
                context.CopilotActivities.Add(editDoc);
                context.CopilotActivities.Add(new CopilotActivity { Name = "Summarise Document", ActivityType = activityTypeDoc });

                var getHighlights = new CopilotActivity { Name = ACTIVITY_NAME_GET_HIGHLIGHTS, ActivityType = activityTypeMeeting };       // Need this later
                context.CopilotActivities.Add(getHighlights);
                context.CopilotActivities.Add(new CopilotActivity { Name = "Get decisions made", ActivityType = activityTypeMeeting });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Get open items", ActivityType = activityTypeMeeting });

                context.CopilotActivities.Add(new CopilotActivity { Name = "Ask question", ActivityType = activityTypeChat });
                context.CopilotActivities.Add(new CopilotActivity { Name = "Other", ActivityType = activityTypeOther });

                // Add some base survey pages
                AddTestSurveyPages(context);
#if DEBUG
                await DirtyTestDataHackInserts(context, logger, editDoc, getHighlights);
                await context.SaveChangesAsync();

                await FakeDataGen.GenerateFakeCopilotFor(defaultUserUPN, context, logger);
#endif
                await context.SaveChangesAsync();

                // Install profiling extensions
                logger.LogInformation("Adding profiling extension");

                await ExecEmbeddedSql("Entities.DB.Profiling.CreateSchema.Profiling-01-CommandExecute.sql", context, logger);
                await ExecEmbeddedSql("Entities.DB.Profiling.CreateSchema.Profiling-02-IndexOptimize.sql", context, logger);
                await ExecEmbeddedSql("Entities.DB.Profiling.CreateSchema.Profiling-03-CreateSchema.sql", context, logger);

                logger.LogInformation("Profiling SQL extension installed");
            }
            else
            {
                logger.LogWarning("No default user set, skipping base data");
            }
        }
    }

    private static async Task ExecEmbeddedSql(string resourceName, DataContext context, ILogger logger)
    {
        logger.LogInformation($"Executing SQL from {resourceName}");
        var script = ResourceUtils.ReadResource(typeof(DbInitialiser).Assembly, resourceName);

        var statements = SplitSqlStatements(script);
        foreach (var statement in statements)
            await context.Database.ExecuteSqlRawAsync(statement);
    }


    // https://stackoverflow.com/questions/18596876/go-statements-blowing-up-sql-execution-in-net
    private static IEnumerable<string> SplitSqlStatements(string sqlScript)
    {
        // Make line endings standard to match RegexOptions.Multiline
        sqlScript = Regex.Replace(sqlScript, @"(\r\n|\n\r|\n|\r)", "\n");

        // Split by "GO" statements
        var statements = Regex.Split(
                sqlScript,
                @"^[\t ]*GO[\t ]*\d*[\t ]*(?:--.*)?$",
                RegexOptions.Multiline |
                RegexOptions.IgnorePatternWhitespace |
                RegexOptions.IgnoreCase);

        // Remove empties, trim, and return
        return statements
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim(' ', '\n'));
    }

    private static async Task DirtyTestDataHackInserts(DataContext context, ILogger logger, CopilotActivity editDocCopilotActivity, CopilotActivity getHighlightsCopilotActivity)
    {
        var rnd = new Random();
        logger.LogInformation("Adding debugging test data");

        var testCompany = new CompanyName { Name = "Contoso" };
        var testJobTitle = new UserJobTitle { Name = "Tester" };
        var testOfficeLocation = new UserOfficeLocation { Name = "Test Office" };

        // Generate some fake departments
        var allDepartments = new List<UserDepartment>();
        List<string> departments =
        [
            "Marketing",
                    "Sales",
                    "Finance",
                    "Human Resources",
                    "Research and Development",
                    "Production",
                    "Quality Assurance",
                    "Customer Service",
                    "Logistics",
                    "Legal",
                ];
        allDepartments.AddRange(departments.Select(d => new UserDepartment { Name = d }));
        context.UserDepartments.AddRange(allDepartments);

        // Add some fake users
        var allUsers = new List<User>();
        for (int i = 0; i < 10; i++)
        {
            var testUser = new User
            {
                UserPrincipalName = $"user-{i}",
                Department = allDepartments[rnd.Next(0, allDepartments.Count - 1)]
            };
            allUsers.Add(testUser);
        }
        context.Users.AddRange(allUsers);
        await context.SaveChangesAsync();

        foreach (var u in allUsers)
        {
            await FakeDataGen.GenerateFakeOfficeActivityFor(u.UserPrincipalName, DateTime.Now, context, logger);
            await context.SaveChangesAsync();
        }

        // Add fake meetings
        var allEvents = new List<CommonAuditEvent>();
        var meetingNames = new List<string>()
                {
                    "Project X: Final Review",
                    "Weekly Team Sync",
                    "Monthly Team Sync",
                    "Customer Feedback Session",
                    "Brainstorming for New Campaign",
                    "Quarterly Performance Review",
                    "Budget Planning Meeting",
                    "Product Launch Strategy",
                    "Training Workshop",
                    "Social Media Analytics",
                    "Happy Hour with Colleagues 🍻"
                };

        var allMeetingEvents = new List<CopilotEventMetadataMeeting>();     // Needed for when we just add teams event feedback, so we don't have exactly 50-50 meetings and files

        var meetingOp = new EventOperation { Name = "Meeting op" };
        foreach (var m in meetingNames)
        {
            var testMeetingEvent = new CopilotEventMetadataMeeting
            {
                RelatedChat = new CopilotChat
                {
                    AppHost = "DevBox",
                    AuditEvent = new CommonAuditEvent
                    {
                        User = allUsers[rnd.Next(0, allUsers.Count - 1)],
                        Id = Guid.NewGuid(),
                        TimeStamp = DateTime.Now.AddDays(allMeetingEvents.Count * -1),
                        Operation = meetingOp
                    }
                },
                OnlineMeeting = new OnlineMeeting { Name = m, MeetingId = "Join Link" }
            };
            context.CopilotEventMetadataMeetings.Add(testMeetingEvent);
            allEvents.Add(testMeetingEvent.RelatedChat.AuditEvent);
            allMeetingEvents.Add(testMeetingEvent);
        }

        var filenames = new List<string>()
                {
                    "Report.docx",
                    "Invoice.pdf",
                    "Presentation.pptx",
                    "Resume.docx",
                    "Budget.xlsx",
                    "Contract.pdf",
                    "Proposal.docx",
                    "Agenda.docx",
                    "Newsletter.pdf",
                    "Summary.pptx"
                };

        // Fake file events
        const string SITE_URL = "https://devbox.sharepoint.com";
        var site = await context.Sites.Where(s => s.UrlBase == SITE_URL).FirstOrDefaultAsync();
        if (site == null)
        {
            site = new Entities.SP.Site { UrlBase = SITE_URL };
            context.Sites.Add(site);
            await context.SaveChangesAsync();
        }

        var fileOp = context.EventOperations.Where(o => o.Name.Contains("File op")).FirstOrDefault() ?? new EventOperation { Name = "File op" };
        foreach (var f in filenames)
        {
            var testFileName = new SPEventFileName { Name = f };
            var testFileEvent = new CopilotEventMetadataFile
            {
                RelatedChat = new CopilotChat
                {
                    AppHost = "DevBox",
                    AuditEvent = new CommonAuditEvent
                    {
                        User = allUsers[rnd.Next(0, allUsers.Count - 1)],
                        Id = Guid.NewGuid(),
                        TimeStamp = DateTime.Now.AddDays(allEvents.Count * -2),
                        Operation = fileOp
                    },

                },
                FileName = testFileName,
                FileExtension = GetSPEventFileExtension(f.Split('.').Last()),
                Url = new Entities.SP.Url { FullUrl = $"https://devbox.sharepoint.com/Docs/{f}" },
                Site = site,
            };
            context.CopilotEventMetadataFiles.Add(testFileEvent);
            allEvents.Add(testFileEvent.RelatedChat.AuditEvent);
        }

        // Add some "averagely happy" fake survey responses for meetings and documents
        const int daysback = 60;
        for (int i = 0; i < daysback; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now, i, 2, 4, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Averagely happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
            AddMeetingAndFileEvent(DateTime.Now, i, 2, 4, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Averagely happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
        }

        // Add some "very unhappy" fake survey responses from earlier on
        for (int i = 0; i < 5; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now.AddDays(daysback * -1), i, 0, 1, context, allUsers, rnd, editDocCopilotActivity, getHighlightsCopilotActivity, "Not happy", allEvents[rnd.Next(0, allEvents.Count - 1)]);
        }
        // Add some "very happy" fake survey responses for meetings and documents. Use Teams events for the feedback
        for (int i = 0; i < 10; i++)
        {
            AddMeetingAndFileEvent(DateTime.Now, i, 4, 5, context, allUsers, rnd, editDocCopilotActivity,
                getHighlightsCopilotActivity, "Very happy",
                allMeetingEvents[rnd.Next(0, allMeetingEvents.Count - 1)].RelatedChat.AuditEvent);
        }
    }

    public static void AddTestSurveyPages(DataContext dataContext)
    {
        var cardContent = new JObject
        {
            ["type"] = $"AdaptiveCard",
            ["version"] = "1.3",
            ["body"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "TextBlock",
                            ["text"] = "Page 1/2 - Extra info",
                            ["wrap"] = true
                        }
                    },
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json"
        };
        var page1 = new SurveyPageDB { Name = "Page 1", PageIndex = 1, IsPublished = true, AdaptiveCardTemplateJson = cardContent.ToString() };
        page1.Questions.AddRange([
            new SurveyQuestionDefinitionDB
            {
                QuestionText = "One word to describe copilot?",
                ForSurveyPage = page1, DataType = QuestionDatatype.String,
                OptimalAnswerValue = null
            },
            new SurveyQuestionDefinitionDB
            {
                QuestionText = "How many minutes has copilot saved you this time?",
                ForSurveyPage = page1, DataType = QuestionDatatype.Int,
                OptimalAnswerValue = "0",
                OptimalAnswerLogicalOp = LogicalOperator.GreaterThan,
                QuestionId = "MinutesSaved"
            },
        ]);

        // Override content body
        cardContent["body"] = new JArray
        {
            new JObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Page 2/2 - Do you value Copilot in O365?",
                ["wrap"] = true
            }
        };
        var page2 = new SurveyPageDB { Name = "Page 2", PageIndex = 2, IsPublished = true, AdaptiveCardTemplateJson = cardContent.ToString() };
        page2.Questions.AddRange([
            new SurveyQuestionDefinitionDB
            {
                QuestionText = "Does copilot help you be more productive generally?",
                ForSurveyPage = page2, DataType = QuestionDatatype.Bool,
                OptimalAnswerValue = "true",
                OptimalAnswerLogicalOp = LogicalOperator.Equals,
                QuestionId = "MakesGenerallyProductive"
            },
        ]);

        dataContext.SurveyPages.AddRange([page1, page2]);
    }

    static List<SPEventFileExtension> _sPEventFileExtensions = new();
    static SPEventFileExtension GetSPEventFileExtension(string ext)
    {
        var e = _sPEventFileExtensions.FirstOrDefault(e => e.Name == ext);
        if (e == null)
        {
            e = new SPEventFileExtension { Name = ext };
            _sPEventFileExtensions.Add(e);
        }
        return e;
    }

    private static void AddMeetingAndFileEvent(DateTime from, int i, int ratingFrom, int ratingTo, DataContext context, List<User> allUsers,
        Random rnd, CopilotActivity docActivity, CopilotActivity meetingActivity, string responseCommentPrefix,
        CommonAuditEvent related)
    {
        var dt = from.AddDays(i * -1);
        var testFileOpResponse = new SurveyGeneralResponseDB
        {
            OverrallRating = rnd.Next(ratingFrom, ratingTo),
            Requested = dt,
            Responded = dt.AddMinutes(i),
            User = allUsers[rnd.Next(0, allUsers.Count)],
        };
        context.SurveyGeneralResponses.Add(testFileOpResponse);
        context.SurveyResponseActivityTypes.Add(new UserSurveyResponseActivityType { CopilotActivity = docActivity, UserSurveyResponse = testFileOpResponse });

        var testMeetingResponse = new SurveyGeneralResponseDB
        {
            OverrallRating = rnd.Next(1, 5),
            Requested = dt,
            Responded = dt.AddMinutes(i),
            User = allUsers[rnd.Next(0, allUsers.Count)],
            RelatedEvent = related
        };
        context.SurveyGeneralResponses.Add(testMeetingResponse);
        context.SurveyResponseActivityTypes.Add(new UserSurveyResponseActivityType { CopilotActivity = meetingActivity, UserSurveyResponse = testMeetingResponse });
    }
}
