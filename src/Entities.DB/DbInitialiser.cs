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
    public static async Task EnsureInitialised(DataContext context, ILogger logger, string? defaultUserUPN, bool insertDebug)
    {
        var createdNewDb = await context.Database.EnsureCreatedAsync();

        if (createdNewDb)
        {
            logger.LogInformation("Database created");


            if (insertDebug)
            {
                // Insert fake user metadata

                logger.LogInformation("Adding debugging test data");


                // Insert fake job titles
                var fakeJobTitles = new List<UserJobTitle>
                    {
                        new UserJobTitle { Name = "Software Engineer" },
                        new UserJobTitle { Name = "Product Manager" },
                        new UserJobTitle { Name = "Data Analyst" },
                        new UserJobTitle { Name = "Sales Representative" },
                        new UserJobTitle { Name = "Marketing Specialist" }
                    };
                context.UserJobTitles.AddRange(fakeJobTitles);

                // Insert fake office locations
                var fakeUserOfficeLocations = new List<UserOfficeLocation>
                    {
                        new UserOfficeLocation { Name = "Head Office" },
                        new UserOfficeLocation { Name = "Remote" },
                        new UserOfficeLocation { Name = "Branch Office" },
                        new UserOfficeLocation { Name = "Satellite Office" }
                    };
                context.UserOfficeLocations.AddRange(fakeUserOfficeLocations);

                // Insert fake company names
                var fakeCompanyNames = new List<CompanyName>
                    {
                        new CompanyName { Name = "Contoso" },
                        new CompanyName { Name = "Fabrikam" },
                        new CompanyName { Name = "Northwind Traders" },
                        new CompanyName { Name = "Adventure Works" }
                    };
                context.CompanyNames.AddRange(fakeCompanyNames);

                // Insert fake departments
                var fakeDepartments = new List<UserDepartment>
                    {
                        new UserDepartment { Name = "Engineering" },
                        new UserDepartment { Name = "Sales" },
                        new UserDepartment { Name = "Marketing" },
                        new UserDepartment { Name = "Finance" },
                        new UserDepartment { Name = "Human Resources" }
                    };
                context.UserDepartments.AddRange(fakeDepartments);
                await context.SaveChangesAsync();


                // Insert new fake users that've used fake agents
                await InsertFakeUsersWithAgents(context, logger);

                await DirtyTestDataHackInserts(context, logger);
                await context.SaveChangesAsync();
            }

            if (defaultUserUPN != null)
            {
                await GenerateTestDataForUser(defaultUserUPN, context, logger, insertDebug);

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

    private static async Task GenerateTestDataForUser(string defaultUserUPN, DataContext context, ILogger logger, bool insertDebug)
    {
        logger.LogInformation("Creating default user");
        var defaultUser = new User
        {
            UserPrincipalName = defaultUserUPN
        };
        context.Users.Add(defaultUser);
        await context.SaveChangesAsync();

        // Add some base survey pages
        AddTestSurveyPages(context);
        if (insertDebug)
        {
            // Generate some fake data
            await FakeDataGen.GenerateFakeCopilotFor(defaultUserUPN, context, logger);
            await FakeDataGen.GenerateFakeOfficeActivityFor(defaultUserUPN, DateTime.Now, context, logger);
        }
        else
        {
            logger.LogInformation("Skipping debugging test data");
        }

        await context.SaveChangesAsync();
    }

    private static async Task InsertFakeUsersWithAgents(DataContext context, ILogger logger)
    {
        logger.LogInformation("Inserting fake users with agents");
        var rnd = new Random();
        var allUsers = await context.Users.ToListAsync();

        var departments = await context.UserDepartments.ToListAsync();
        var jobTitles = await context.UserJobTitles.ToListAsync();
        var officeLocations = await context.UserOfficeLocations.ToListAsync();
        var companyNames = await context.CompanyNames.ToListAsync();

        if (departments.Count == 0 || jobTitles.Count == 0 ||
            officeLocations.Count == 0 || companyNames.Count == 0)
        {
            logger.LogWarning("Not enough lookup data to create fake users. Skipping user creation.");
            return;
        }

        // Add fake users
        for (int i = 0; i < 10; i++)
        {
            // Get random department, job title, office location, and company name
            if (context.UserDepartments.Count() == 0 || context.UserJobTitles.Count() == 0 ||
                context.UserOfficeLocations.Count() == 0 || context.CompanyNames.Count() == 0)
            {
                logger.LogWarning("Not enough lookup data to create fake users. Skipping user creation.");
                return;
            }

            var userNumber = allUsers.Count + i + 1;


            var testUser = new User
            {
                UserPrincipalName = $"user-{userNumber}",
                Department = departments[rnd.Next(departments.Count)],
                JobTitle = jobTitles[rnd.Next(jobTitles.Count)],
                OfficeLocation = officeLocations[rnd.Next(officeLocations.Count)],
                CompanyName = companyNames[rnd.Next(companyNames.Count)],
            };

            allUsers.Add(testUser);
            context.Users.Add(testUser);
        }

        var agentCount = await context.CopilotAgents.CountAsync();

        // Add some fake agents
        for (int i = 0; i < 10; i++)
        {
            var agentNumber = agentCount + i + 1;
            var testAgent = new CopilotAgent
            {
                AgentID = $"agent-{agentNumber}",
                Name = $"Test Agent {agentNumber}",
            };
            context.CopilotAgents.Add(testAgent);
        }

        // Add some fake chats with agents
        var allChats = new List<CopilotChat>();
        foreach (var chat in allUsers) {
            for (int i = 0; i < rnd.Next(1, 10); i++)
            {
                var testChat = new CopilotChat
                {
                    AppHost = "DevBox",
                    Agent = context.CopilotAgents.OrderBy(a => EF.Functions.Random()).FirstOrDefault(),
                    AuditEvent = new CommonAuditEvent
                    {
                        User = chat,
                        Id = Guid.NewGuid(),
                        TimeStamp = DateTime.Now.AddDays(i * -1),
                        Operation = new EventOperation { Name = "Chat op" }
                    }
                };
                allChats.Add(testChat);
            }
        }

        await context.SaveChangesAsync();
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

    private static async Task DirtyTestDataHackInserts(DataContext context, ILogger logger)
    {
        var rnd = new Random();
        logger.LogInformation("Adding debugging test data");

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
        context.CopilotActivities.Add(new CopilotActivity { Name = "Summarise Document", ActivityType = activityTypeDoc });

        var getHighlightsCopilotActivity = new CopilotActivity { Name = ACTIVITY_NAME_GET_HIGHLIGHTS, ActivityType = activityTypeMeeting };       // Need this later
        context.CopilotActivities.Add(getHighlightsCopilotActivity);
        context.CopilotActivities.Add(new CopilotActivity { Name = "Get decisions made", ActivityType = activityTypeMeeting });
        context.CopilotActivities.Add(new CopilotActivity { Name = "Get open items", ActivityType = activityTypeMeeting });

        context.CopilotActivities.Add(new CopilotActivity { Name = "Ask question", ActivityType = activityTypeChat });
        context.CopilotActivities.Add(new CopilotActivity { Name = "Other", ActivityType = activityTypeOther });


        var editDocCopilotActivity = new CopilotActivity { Name = ACTIVITY_NAME_EDIT_DOC, ActivityType = activityTypeDoc };       // Need this later
        context.CopilotActivities.Add(editDocCopilotActivity);

        var testCompany = new CompanyName { Name = "Contoso" };
        var testJobTitle = new UserJobTitle { Name = "Tester" };
        var testOfficeLocation = new UserOfficeLocation { Name = "Test Office" };

        var allUsers = await context.Users.ToListAsync();
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
