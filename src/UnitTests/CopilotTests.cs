﻿using ActivityImporter.Engine;
using ActivityImporter.Engine.ActivityAPI;
using ActivityImporter.Engine.ActivityAPI.Copilot;
using ActivityImporter.Engine.ActivityAPI.Models;
using Common.DataUtils;
using Common.Engine.Surveys;
using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Microsoft.EntityFrameworkCore;
using UnitTests.FakeLoaderClasses;

namespace UnitTests;

[TestClass]
public class CopilotTests : AbstractTest
{
    [TestMethod]
    public void SurveyPendingActivitiesGetNext()
    {
        var spa = new SurveyPendingActivities();
        Assert.IsNotNull(spa.FileEvents);
        Assert.IsNotNull(spa.MeetingEvents);
        Assert.IsNull(spa.GetNext());

        var firstFile = new CopilotEventMetadataFile { RelatedChat = new CopilotChat { AuditEvent = new CommonAuditEvent { TimeStamp = DateTime.Now.AddDays(-1) } } };
        var secondFile = new CopilotEventMetadataFile { RelatedChat = new CopilotChat { AuditEvent = new CommonAuditEvent { TimeStamp = DateTime.Now } } };
        spa.FileEvents.AddRange([firstFile, secondFile]);

        Assert.IsTrue(spa.GetNext() == firstFile);



        var firstMeeting = new CopilotEventMetadataMeeting { RelatedChat = new CopilotChat { AuditEvent = new CommonAuditEvent { TimeStamp = DateTime.Now.AddDays(-1) } } };
        var secondMeeting = new CopilotEventMetadataMeeting { RelatedChat = new CopilotChat { AuditEvent = new CommonAuditEvent { TimeStamp = DateTime.Now } } };
        spa.MeetingEvents.AddRange(new CopilotEventMetadataMeeting[] { firstMeeting, secondMeeting });

        Assert.IsTrue(spa.GetNext() == firstMeeting);


    }

    [TestMethod]
    public async Task FindNewSurveyEventsAndLogSurveyRequested()
    {
        var sm = new SurveyManager(new FakeSurveyManagerDataLoader(_config), new FakeSurveyProcessor(), GetLogger<SurveyManager>());

        var testUser = new User { UserPrincipalName = _config.TestCopilotEventUPN };
        var r = await sm.FindNewSurveyEvents(testUser);

        Assert.IsTrue(r.MeetingEvents.Count == 1);
        Assert.IsTrue(r.FileEvents.Count == 1);
        Assert.IsNotNull(r.MeetingEvents[0]);
        Assert.IsNotNull(r.FileEvents[0]);

        // Request survey
        await sm.Loader.LogSurveyRequested(r.MeetingEvents[0].RelatedChat.AuditEvent);
        await sm.Loader.LogSurveyRequested(r.FileEvents[0].RelatedChat.AuditEvent);

        // Survey again
        var r2 = await sm.FindNewSurveyEvents(testUser);

        // Results should be the same as we've already registered the surveys
        Assert.IsTrue(r2.MeetingEvents.Count == 0);
        Assert.IsTrue(r2.FileEvents.Count == 0);
    }

    // https://learn.microsoft.com/en-us/office/office-365-management-api/copilot-schema
    [TestMethod]
    public async Task CopilotEventManagerSaveTest()
    {
        await ClearEvents();

        var copilotEventAdaptor = new CopilotAuditEventManager(_config.ConnectionStrings.SQL, new FakeCopilotEventAdaptor(), _logger);

        var commonEventDocEdit = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Document Edit" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test doc user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonEventChat = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Chat or something" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test chat user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonEventMeeting = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Meeting Op" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test meeting user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonOutlook = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Outlook Op" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test outlook user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };

        // Audit metadata for our tests
        var meeting = new CopilotEventData
        {
            AppHost = "test",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2",   // Needs to be real
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMS_MEETING
                }
            }
        };
        var docEvent = new CopilotEventData
        {
            AppHost = "Word",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = _config.TestCopilotDocContextIdSpSite,
                    Type = _config.TeamSiteFileExtension
                }
            }
        };
        var teamsChat = new CopilotEventData
        {
            AppHost = "Teams",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:somechatthread@thread.v2",
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMS_CHAT
                }
            }
        };

        var outlook = new CopilotEventData
        {
            AppHost = "Outlook",
            AccessedResources = new List<AccessedResource>
            {
                new AccessedResource { Type = "http://schema.skype.com/HyperLink" }
            },
        };


        // Check counts before and after
        var fileEventsPreCount = await _db.CopilotEventMetadataFiles.CountAsync();
        var meetingEventsPreCount = await _db.CopilotEventMetadataMeetings.CountAsync();
        var allCopilotEventsPreCount = await _db.CopilotChats.CountAsync();

        // Save common events as they are required for the foreign key - the common event is saved before CopilotAuditEventManager runs on the metadata
        _db.AuditEventsCommon.Add(commonEventDocEdit);
        _db.AuditEventsCommon.Add(commonEventMeeting);
        _db.AuditEventsCommon.Add(commonEventChat);
        _db.AuditEventsCommon.Add(commonOutlook);
        await _db.SaveChangesAsync();

        // Save events
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = meeting }, commonEventMeeting);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = docEvent }, commonEventDocEdit);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = teamsChat }, commonEventChat);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = outlook }, commonOutlook);
        await copilotEventAdaptor.CommitAllChanges();


        // Check event does not have agent
        var commonEventDocEditReloaded = await _db.CopilotEventMetadataFiles
            .Include(x => x.RelatedChat)
            .ThenInclude(x => x.Agent)
            .FirstOrDefaultAsync(x => x.RelatedChat.AuditEvent == commonEventDocEdit);
        Assert.IsNotNull(commonEventDocEditReloaded);
        Assert.IsNull(commonEventDocEditReloaded.RelatedChat.Agent);

        // Verify counts have increased
        var fileEventsPostCount = await _db.CopilotEventMetadataFiles.CountAsync();
        var meetingEventsPostCount = await _db.CopilotEventMetadataMeetings.CountAsync();
        var allCopilotEventsPostCount = await _db.CopilotChats.CountAsync();

        Assert.IsTrue(fileEventsPostCount == fileEventsPreCount + 1);
        Assert.IsTrue(meetingEventsPostCount == meetingEventsPreCount + 1);
        Assert.IsTrue(allCopilotEventsPostCount == allCopilotEventsPreCount + 4, "Unexpected save result without agent"); // 4 new events - 1 meeting, 1 file, 1 chat, 1 outlook

    }

    [TestMethod]
    public async Task CopilotEventManagerWithAgentSaveTest()
    {
        await ClearEvents();

        var copilotEventAdaptor = new CopilotAuditEventManager(_config.ConnectionStrings.SQL, new FakeCopilotEventAdaptor(), _logger);

        var commonEventDocEdit = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Document Edit" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test doc user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonEventChat = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Chat or something" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test chat user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonEventMeeting = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Meeting Op" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test meeting user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };
        var commonOutlook = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Outlook Op" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test outlook user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };

        // Audit metadata for our tests
        var meeting = new CopilotEventData
        {
            AppHost = "test",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2",   // Needs to be real
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMS_MEETING
                }
            }
        };
        var docEvent = new CopilotEventData
        {
            AppHost = "Word",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = _config.TestCopilotDocContextIdSpSite,
                    Type = _config.TeamSiteFileExtension
                }
            }
        };
        var teamsChat = new CopilotEventData
        {
            AppHost = "Teams",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:somechatthread@thread.v2",
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMS_CHAT
                }
            }
        };

        var outlook = new CopilotEventData
        {
            AppHost = "Outlook",
            AccessedResources = new List<AccessedResource>
            {
                new AccessedResource { Type = "http://schema.skype.com/HyperLink" }
            },
        };


        // Check counts before and after
        var fileEventsPreCount = await _db.CopilotEventMetadataFiles.CountAsync();
        var meetingEventsPreCount = await _db.CopilotEventMetadataMeetings.CountAsync();
        var allCopilotEventsPreCount = await _db.CopilotChats.CountAsync();

        // Save common events as they are required for the foreign key - the common event is saved before CopilotAuditEventManager runs on the metadata
        _db.AuditEventsCommon.Add(commonEventDocEdit);
        _db.AuditEventsCommon.Add(commonEventMeeting);
        _db.AuditEventsCommon.Add(commonEventChat);
        _db.AuditEventsCommon.Add(commonOutlook);
        await _db.SaveChangesAsync();

        var preAgentsCount = await _db.CopilotAgentTypes.CountAsync();
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = meeting, AgentName = "Test Agent Meeting " + DateTime.Now.Ticks, AgentId = "Unit testing1 " + DateTime.Now.Ticks }, commonEventMeeting);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = docEvent, AgentName = "Test Agent Doc " + DateTime.Now.Ticks, AgentId = "Unit testing2 " + DateTime.Now.Ticks }, commonEventDocEdit);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = teamsChat, AgentName = "Test Agent Chat " + DateTime.Now.Ticks, AgentId = "Unit testing3 " + DateTime.Now.Ticks }, commonEventChat);
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = outlook, AgentName = "Test Agent Outlook " + DateTime.Now.Ticks, AgentId = "Unit testing4 " + DateTime.Now.Ticks }, commonOutlook);
        await copilotEventAdaptor.CommitAllChanges();

        // Check agent counts include newly defined agents
        var postAgents = await _db.CopilotAgentTypes.ToListAsync();
        Assert.IsTrue(postAgents.Count == preAgentsCount + 4); // 4 new agents - 1 per test

        Assert.IsTrue(postAgents.Any(x => x.Name.Contains("Test Agent Meeting")));
        Assert.IsTrue(postAgents.Any(x => x.Name.Contains("Test Agent Doc")));
        Assert.IsTrue(postAgents.Any(x => x.Name.Contains("Test Agent Chat")));
        Assert.IsTrue(postAgents.Any(x => x.Name.Contains("Test Agent Outlook")));

        // Check event has agent
        var commonEventDocEditReloaded = await _db.CopilotEventMetadataFiles
            .Include(x => x.RelatedChat)
            .ThenInclude(x => x.Agent)
            .FirstOrDefaultAsync(x => x.RelatedChat.AuditEvent == commonEventDocEdit);
        Assert.IsNotNull(commonEventDocEditReloaded);
        Assert.IsNotNull(commonEventDocEditReloaded.RelatedChat.Agent);
        Assert.IsTrue(commonEventDocEditReloaded.RelatedChat.Agent.Name.Contains("Test Agent Doc"));
        Assert.IsTrue(commonEventDocEditReloaded.RelatedChat.Agent.AgentID.Contains("Unit testing"));
    }

    [TestMethod]
    public async Task CopilotEventManagerAgentNameUpdateSaveTest()
    {
        await ClearEvents();

        var copilotEventAdaptor = new CopilotAuditEventManager(_config.ConnectionStrings.SQL, new FakeCopilotEventAdaptor(), _logger);

        var commonEventChat1 = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Chat or something" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test chat user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };

        var commonEventChat2 = new CommonAuditEvent
        {
            TimeStamp = DateTime.Now,
            Operation = new EventOperation { Name = "Chat or something" + DateTime.Now.Ticks },
            User = new User { AzureAdId = "test", UserPrincipalName = "test chat user " + DateTime.Now.Ticks },
            Id = Guid.NewGuid()
        };


        var teamsChat = new CopilotEventData
        {
            AppHost = "Teams",
            Contexts = new List<Context>
            {
                new Context
                {
                    Id = "https://microsoft.teams.com/threads/19:somechatthread@thread.v2",
                    Type = ActivityImportConstants.COPILOT_CONTEXT_TYPE_TEAMS_CHAT
                }
            }
        };

        // Save common events as they are required for the foreign key - the common event is saved before CopilotAuditEventManager runs on the metadata
        _db.AuditEventsCommon.AddRange([commonEventChat1, commonEventChat2]);
        await _db.SaveChangesAsync();

        // Save event with agent name 1
        var agentId = "Unit testing3 " + DateTime.Now.Ticks;
        var agentName = "Test Agent Chat " + DateTime.Now.Ticks;
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = teamsChat, AgentName = agentName, AgentId = agentId }, commonEventChat1);
        await copilotEventAdaptor.CommitAllChanges();

        // Check event has right agent details
        var commonEventDocEditReloaded = await _db.CopilotChats
            .Include(x => x.Agent)
            .FirstOrDefaultAsync(x => x.AuditEvent == commonEventChat1);
        Assert.IsNotNull(commonEventDocEditReloaded);
        Assert.IsTrue(commonEventDocEditReloaded.Agent.AgentID == agentId);
        Assert.IsTrue(commonEventDocEditReloaded.Agent.Name == agentName);

        // Save another event with new agent name but same agent ID
        agentName = "Test Agent Chat 2 " + DateTime.Now.Ticks;
        var newAgentName = "Test Agent New Name " + DateTime.Now.Ticks;
        await copilotEventAdaptor.SaveSingleCopilotEventToSqlStaging(new CopilotAuditLogContent { CopilotEventData = teamsChat, AgentName = newAgentName, AgentId = agentId }, commonEventChat2);
        await copilotEventAdaptor.CommitAllChanges();

        // Check event has right agent details
        var commonEventDocEditReloaded2 = await _db.CopilotChats
            .Include(x => x.Agent)
            .FirstOrDefaultAsync(x => x.AuditEvent == commonEventChat2);
        await _db.Entry(commonEventDocEditReloaded2!.Agent).ReloadAsync();
        Assert.IsNotNull(commonEventDocEditReloaded2);
        Assert.IsTrue(commonEventDocEditReloaded2.Agent.AgentID == agentId);
        Assert.IsTrue(commonEventDocEditReloaded2.Agent.Name == newAgentName);
    }

    async Task ClearEvents()
    {

        // Clear events for test
        _db.CopilotEventMetadataFiles.RemoveRange(_db.CopilotEventMetadataFiles);
        _db.CopilotEventMetadataMeetings.RemoveRange(_db.CopilotEventMetadataMeetings);
        _db.CopilotChats.RemoveRange(_db.CopilotChats);

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Tests we can load metadata from Graph
    /// </summary>
    [TestMethod]
    public async Task GraphCopilotMetadataLoaderTests()
    {
        var auth = new GraphAppIndentityOAuthContext(_logger, _config.ImportAuthConfig.ClientId, _config.ImportAuthConfig.TenantId, _config.ImportAuthConfig.ClientSecret, string.Empty, false);
        await auth.InitClientCredential();

        var loader = new GraphFileMetadataLoader(new Microsoft.Graph.GraphServiceClient(auth.Creds), _logger);

        // Test a file from users OneDrive (my site)
        var mySiteFileInfo = await loader.GetSpoFileInfo(_config.TestCopilotDocContextIdMySites, _config.TestCopilotEventUPN);
        Assert.AreEqual(mySiteFileInfo?.Extension, _config.MySitesFileExtension);
        Assert.AreEqual(mySiteFileInfo?.Filename, _config.MySitesFileName);
        Assert.AreEqual(mySiteFileInfo?.Url, _config.MySitesFileUrl);

        // Test a file from a team site
        var spSiteFileInfo = await loader.GetSpoFileInfo(_config.TestCopilotDocContextIdSpSite, _config.TestCopilotEventUPN);
        Assert.AreEqual(spSiteFileInfo?.Extension, _config.TeamSiteFileExtension);
        Assert.AreEqual(spSiteFileInfo?.Filename, _config.TeamSitesFileName);
        Assert.AreEqual(spSiteFileInfo?.Url, _config.TeamSiteFileUrl);

        // Test a call
        if (!string.IsNullOrEmpty(_config.TestCallThreadId))
        {
            var userId = await loader.GetUserIdFromUpn(_config.TestCopilotEventUPN);
            var meeting = await loader.GetMeetingInfo(StringUtils.GetOnlineMeetingId(_config.TestCallThreadId!, userId), userId);
            Assert.IsNotNull(meeting);
        }
    }
}
