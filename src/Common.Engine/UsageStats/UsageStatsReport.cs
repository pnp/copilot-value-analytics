using Entities.DB.Models;

namespace Common.Engine.UsageStats;

public class UsageStatsReport
{
    public UsageStatsReport(IEnumerable<IActivitiesWeeklyRecord> fromData) : this(fromData, new UsageStatsReportFilter())
    {
    }
    public UsageStatsReport(IEnumerable<IActivitiesWeeklyRecord> fromData, UsageStatsReportFilter filter)
    {
        var filteredUsers = fromData
            .Select(x => x.User)
            .Distinct()
            .ByFilter(filter)
            .ToList();

        var filteredData = fromData
            .Where(x => filteredUsers.Contains(x.User))
            .ToList();

        this.UniqueActivities = filteredData
            .Select(x => x.Metric)
            .Distinct()
            .OrderDescending()
            .ToList();

        this.Dates = filteredData
            .Select(x => x.MetricDate)
            .Distinct()
            .OrderDescending()
            .ToList();

        var usersResults = new List<EntityWithScore<ITrackedUser>>();
        foreach (var user in filteredUsers)
        {
            var score = filteredData
                .Where(x => x.User == user)
                .Sum(x => x.Sum);
            usersResults.Add(new EntityWithScore<ITrackedUser>(user, score));
        }
        this.UsersLeague = usersResults.OrderByDescending(u => u.Score).ToList();

        this.CountriesLeague = UsersLeague.BuildLeagueForLookup(x => x.UserCountry);
        this.DepartmentsLeague = UsersLeague.BuildLeagueForLookup(x => x.Department);

    }

    public List<string> UniqueActivities { get; set; } = new();

    public List<DateOnly> Dates { get; set; } = new();

    public List<EntityWithScore<ITrackedUser>> UsersLeague { get; set; } = new();
    public List<EntityWithScore<string>> DepartmentsLeague { get; set; } = new();
    public List<EntityWithScore<string>> CountriesLeague { get; set; } = new();
}
