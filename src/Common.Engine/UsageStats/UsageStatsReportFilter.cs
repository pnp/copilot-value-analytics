using Entities.DB.Models;

namespace Common.Engine.UsageStats;

public class UsageStatsReportFilter
{
    public List<string> InDepartments { get; set; } = new();

    public List<string> InCountries { get; set; } = new();
}


public static class Functions
{
    public static IEnumerable<ITrackedUser> ByFilter(this IEnumerable<ITrackedUser> source, UsageStatsReportFilter filter)
    {
        return source
            .Where(user => user != null)
            .ByDepartments(filter.InDepartments)
            .ByCountries(filter.InCountries);
    }

    static IEnumerable<ITrackedUser> ByDepartments(this IEnumerable<ITrackedUser> source, List<string> inList)
    {
        return source
            .ByProperty(inList, user => user.Department);
    }
    static IEnumerable<ITrackedUser> ByCountries(this IEnumerable<ITrackedUser> source, List<string> inList)
    {
        return source
            .ByProperty(inList, user => user.UserCountry);
    }

    static IEnumerable<ITrackedUser> ByProperty(this IEnumerable<ITrackedUser> source, List<string> inList, Func<ITrackedUser, string?> propertySelector)
    {
        return source
            .Where(user => inList.Count == 0 ||
                (inList.Count > 0 && propertySelector(user) != null && inList.Contains(propertySelector(user)!)));
    }

    public static List<EntityWithScore<string>> BuildLeagueForLookup(this List<EntityWithScore<ITrackedUser>> users, Func<ITrackedUser, string?> propertySelector)
    {
        var results = new List<EntityWithScore<string>>();
        foreach (var lookup in users
            .Select(x => propertySelector(x.Entity))
            .Distinct()
            .Where(x => x != null))
        {
            if (lookup != null)
            {
                var score = users
                    .Where(x => propertySelector(x.Entity) == lookup)
                    .Sum(x => x.Score);
                results.Add(new EntityWithScore<string>(lookup, score));
            }
        }

        return results;
    }
}
