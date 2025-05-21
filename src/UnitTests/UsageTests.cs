using ActivityImporter.Engine.Graph.O365UsageReports;
using ActivityImporter.Engine.Graph.O365UsageReports.ReportLoaders;
using Common.Engine;
using Common.Engine.UsageStats;
using Entities.DB.DbContexts;
using Entities.DB.Entities;
using Entities.DB.Entities.Profiling;
using Entities.DB.Models;
using Microsoft.EntityFrameworkCore;
using UnitTests.FakeLoaderClasses;

namespace UnitTests;

[TestClass]
public class UsageTests : AbstractTest
{
    private TestUser user1 = new TestUser
    {
        UserPrincipalName = "user1@contoso.local",
        CompanyName = "Contoso",
        StateOrProvince = "WA",
        UserCountry = "US",
        OfficeLocation = "Seattle",
        UsageLocation = "US",
        Department = "IT",
        JobTitle = "Developer",
    };
    private TestUser user2 = new TestUser
    {
        UserPrincipalName = "user2",
        CompanyName = "Contoso",
        StateOrProvince = "WA",
        UserCountry = "US",
        OfficeLocation = "Seattle",
        UsageLocation = "US",
        Department = "Engineering",
        JobTitle = "Developer",
    };

    [TestMethod]
    public async Task ReportManagerSqlTests()
    {
        var filter = new LoaderUsageStatsReportFilter
        {
            From = DateTime.UtcNow.AddDays(-7),
            To = DateTime.UtcNow
        };


        var optionsBuilder = new DbContextOptionsBuilder<ProfilingContext>();
        optionsBuilder.UseSqlServer(_config.ConnectionStrings.SQL);

        var db = new ProfilingContext(optionsBuilder.Options);

        // Manually add processed profiling data
        var testUSName = "US-" + DateTime.Now.Ticks;
        db.ActivitiesWeekly.RemoveRange(db.ActivitiesWeekly);
        db.ActivitiesWeekly.Add(new ActivitiesWeekly
        {
            Metric = "Teams Meetings Attended",
            MetricDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Sum = 1,
            User = new User
            {
                UserPrincipalName = "user1-" + DateTime.Now.Ticks,
                UserCountry = new CountryOrRegion { Name = testUSName }
            }
        });
        db.ActivitiesWeekly.Add(new ActivitiesWeekly
        {
            Metric = "Teams Meetings Attended",
            MetricDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Sum = 1,
            User = new User { UserPrincipalName = "user2-" + DateTime.Now.Ticks }
        });
        await db.SaveChangesAsync();


        var dataLoader = new SqlUsageDataLoader(db, GetLogger<SqlUsageDataLoader>());
        await dataLoader.RefreshProfilingStats(4);

        var reportManager = new ReportManager(dataLoader, GetLogger<ReportManager>());
        var report = await reportManager.GetReport(filter);
        Assert.IsNotNull(report);

        Assert.AreEqual(2, report.UsersLeague.Count);

        filter.From = DateTime.UtcNow.AddDays(-1);
        var reportWithNewDateFilter = await reportManager.GetReport(filter);
        Assert.IsNotNull(reportWithNewDateFilter);
        Assert.AreEqual(1, reportWithNewDateFilter.UsersLeague.Count);
    }


    [TestMethod]
    public async Task ReportManagerRefreshSqlTests()
    {
        var filter = new LoaderUsageStatsReportFilter
        {
            From = DateTime.UtcNow.AddDays(-21),        // Week starting
            To = DateTime.UtcNow
        };

        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_config.ConnectionStrings.SQL);
        var db = new DataContext(optionsBuilder.Options);

        var optionsBuilderProfiling = new DbContextOptionsBuilder<ProfilingContext>();
        optionsBuilderProfiling.UseSqlServer(_config.ConnectionStrings.SQL);
        var dbProfiling = new ProfilingContext(optionsBuilderProfiling.Options);

        var dataLoader = new SqlUsageDataLoader(dbProfiling, GetLogger<SqlUsageDataLoader>());

        // Clear down the data
        db.TeamUserActivityLogs.RemoveRange(db.TeamUserActivityLogs);
        await db.SaveChangesAsync();
        await dataLoader.ClearProfilingStats();

        var reportManager = new ReportManager(dataLoader, GetLogger<ReportManager>());

        // Check empty
        var emptyReport = await reportManager.GetReport(filter);
        Assert.IsNotNull(emptyReport);
        Assert.AreEqual(0, emptyReport.UsersLeague.Count);

        // Add new usage data and refresh
        db.TeamUserActivityLogs.Add(new Entities.DB.Entities.UsageReports.GlobalTeamsUserUsageLog
        {
            DateOfActivity = DateTime.UtcNow.AddDays(-14),
            AdHocMeetingsAttendedCount = 1,
            User = new User { UserPrincipalName = "user-1-" + DateTime.Now.Ticks },
        });
        db.OutlookUsageActivityLogs.Add(new Entities.DB.Entities.UsageReports.OutlookUsageActivityLog
        {
            DateOfActivity = DateTime.UtcNow.AddDays(-14),
            User = new User { UserPrincipalName = "user-2-" + DateTime.Now.Ticks },
            ReadCount = 1,
        });
        await db.SaveChangesAsync();

        await dataLoader.RefreshProfilingStats(4);

        // Check the data again. After refresh we should have 2 records
        var report = await reportManager.GetReport(filter);
        Assert.IsNotNull(report);

        Assert.IsTrue(report.UsersLeague.Count > 0);
    }

    [TestMethod]
    public void UsageStatsReportTests()
    {
        var testData = new List<TestActivitiesWeeklyRecord>
        {
            new TestActivitiesWeeklyRecord
            {
                Metric = "Teams Meetings Attended",
                MetricDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Sum = 1,
                User = user1
            },
            new TestActivitiesWeeklyRecord
            {
                Metric = "Teams Team Chats",
                MetricDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                Sum = 2,
                User = user2
            },
            new TestActivitiesWeeklyRecord
            {
                Metric = "Random Office activity",
                MetricDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                Sum = 1,
                User = new TestUser
                {
                    UserPrincipalName = "randomExtraItUser",        // IT department
                    CompanyName = "Contoso",
                    StateOrProvince = "WA",
                    UserCountry = "US",
                    OfficeLocation = "Seattle",
                    UsageLocation = "US",
                    Department = "IT",
                    JobTitle = "Developer"
                }
            },
        };

        var reportAllData = new UsageStatsReport(testData.Cast<IActivitiesWeeklyRecord>());

        Assert.AreEqual(3, reportAllData.UsersLeague.Count);
        Assert.AreEqual(3, reportAllData.UniqueActivities.Count);
        Assert.AreEqual(3, reportAllData.Dates.Count);


        Assert.IsTrue(reportAllData.UniqueActivities.Contains("Teams Meetings Attended"));
        Assert.IsTrue(reportAllData.UniqueActivities.Contains("Teams Team Chats"));
        Assert.AreEqual(DateOnly.FromDateTime(DateTime.UtcNow), reportAllData.Dates[0]);
        Assert.AreEqual(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), reportAllData.Dates[1]);

        var reportUser1 = reportAllData.UsersLeague.Where(u => u.Entity.UserPrincipalName == user1.UserPrincipalName).First();
        var reportUser2 = reportAllData.UsersLeague.Where(u => u.Entity.UserPrincipalName == user2.UserPrincipalName).First();
        var reportRandomUser = reportAllData.UsersLeague.Where(u => u.Entity.UserPrincipalName == "randomExtraItUser").First();

        Assert.AreEqual(1, reportUser1.Score);
        Assert.AreEqual(2, reportUser2.Score);
        Assert.AreEqual(1, reportRandomUser.Score);

        // Check department league
        Assert.AreEqual(2, reportAllData.DepartmentsLeague.Count);
        Assert.IsTrue(reportAllData.DepartmentsLeague.Any(u => u.Entity == "IT"));
        Assert.AreEqual(2, reportAllData.DepartmentsLeague[0].Score);
        Assert.IsTrue(reportAllData.DepartmentsLeague.Any(u=> u.Entity == "Engineering"));
        Assert.AreEqual(2, reportAllData.DepartmentsLeague[1].Score);
        Assert.AreEqual(user1.UserPrincipalName, reportUser1.Entity.UserPrincipalName);
        Assert.AreEqual(user2.UserPrincipalName, reportUser2.Entity.UserPrincipalName);

        var reportDepartmentFilteredData = new UsageStatsReport(testData.Cast<IActivitiesWeeklyRecord>(), new UsageStatsReportFilter { InDepartments = new List<string> { "IT" } });
        Assert.AreEqual(2, reportDepartmentFilteredData.UsersLeague.Count);
        Assert.AreEqual(2, reportDepartmentFilteredData.UniqueActivities.Count);
        Assert.AreEqual(2, reportDepartmentFilteredData.Dates.Count);
        Assert.AreEqual("Teams Meetings Attended", reportDepartmentFilteredData.UniqueActivities[0]);
        Assert.AreEqual(DateOnly.FromDateTime(DateTime.UtcNow), reportDepartmentFilteredData.Dates[0]);
        Assert.AreEqual(1, reportDepartmentFilteredData.UsersLeague[0].Score);
        Assert.AreEqual(user1.UserPrincipalName, reportUser1.Entity.UserPrincipalName);
    }

    [TestMethod]
    public void UserFilterTests()
    {
        var users = new List<TestUser> { user1, user2 };

        var resultsByDepartmentsAndCountries = users.ByFilter(new UsageStatsReportFilter { InDepartments = new List<string> { "IT" }, InCountries = new List<string> { "US" } });
        Assert.AreEqual(1, resultsByDepartmentsAndCountries.Count());

        var resultsNoFilter = users.ByFilter(new UsageStatsReportFilter { InDepartments = new List<string>() });
        Assert.AreEqual(2, resultsNoFilter.Count());
    }


    [TestMethod]
    public void BuildLeagueForLookupTests()
    {
        var testData = new List<EntityWithScore<ITrackedUser>>
        {
            new EntityWithScore<ITrackedUser>(user1, 1),
            new EntityWithScore<ITrackedUser>(user2, 2),
            new EntityWithScore<ITrackedUser>(new TestUser
            {
                UserPrincipalName = "randomExtraItUser",
                CompanyName = "Contoso",
                StateOrProvince = "WA",
                UserCountry = "US",
                OfficeLocation = "Seattle",
                UsageLocation = "US",
                Department = "IT",
                JobTitle = "Developer"
            }, 3)
        };

        var leagueOfCountries = testData.BuildLeagueForLookup(x => x.UserCountry);
        Assert.AreEqual(1, leagueOfCountries.Count);
        Assert.AreEqual("US", leagueOfCountries[0].Entity);
        Assert.AreEqual(6, leagueOfCountries[0].Score);

        var leagueOfDepartments = testData.BuildLeagueForLookup(x => x.Department);
        Assert.AreEqual(2, leagueOfDepartments.Count);
        Assert.AreEqual("IT", leagueOfDepartments[0].Entity);
        Assert.AreEqual(4, leagueOfDepartments[0].Score);
        Assert.AreEqual("Engineering", leagueOfDepartments[1].Entity);
        Assert.AreEqual(2, leagueOfDepartments[1].Score);
    }

    [TestMethod]
    public async Task FakeTeamsActivity()
    {
        var activityLoader = new FakeUserActivityLoader();
        var loader = new TeamsUserDeviceLoader(activityLoader, new SqlUsageReportPersistence(_db, _logger), _logger);
        _db.TeamsUserDeviceUsageLog.RemoveRange(_db.TeamsUserDeviceUsageLog);
        await _db.SaveChangesAsync();

        var p = await loader.LoadAndSaveUsagePages();
        Assert.AreEqual(TeamsUserDeviceLoader.MAX_DAYS_BACK - 1, p.Count);
    }
}

class TestActivitiesWeeklyRecord : IActivitiesWeeklyRecord
{
    public string Metric { get; set; } = string.Empty;
    public DateOnly MetricDate { get; set; }
    public int Sum { get; set; }
    public TestUser User { get; set; } = null!;

    ITrackedUser IActivitiesWeeklyRecord.User => User;
}

class TestUser : ITrackedUser
{
    public string UserPrincipalName { get; set; } = null!;

    public string? CompanyName { get; set; } = null!;

    public string? StateOrProvince { get; set; } = null!;

    public User? Manager { get; set; } = null!;

    public string? UserCountry { get; set; } = null!;

    public string? OfficeLocation { get; set; } = null!;

    public string? UsageLocation { get; set; } = null!;

    public string? Department { get; set; } = null!;

    public string? JobTitle { get; set; } = null!;

    public List<string> Licenses { get; set; } = new List<string>();

    ITrackedUser? ITrackedUser.Manager => Manager;

}
