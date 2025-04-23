using Entities.DB.DbContexts;
using Entities.DB.Entities.Profiling;
using Entities.DB.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Engine.UsageStats;

public class SqlUsageDataLoader(ProfilingContext context, ILogger<SqlUsageDataLoader> logger) : IUsageDataLoader
{
    public async Task ClearProfilingStats()
    {
        logger.LogInformation("Clearing profiling stats");
        await context.Database.ExecuteSqlRawAsync("truncate table [profiling].ActivitiesWeekly");
        await context.Database.ExecuteSqlRawAsync("truncate table [profiling].ActivitiesWeeklyColumns");
        await context.Database.ExecuteSqlRawAsync("truncate table [profiling].UsageWeekly");
    }

    public async Task<IEnumerable<IActivitiesWeeklyRecord>> LoadRecords(LoaderUsageStatsReportFilter filter)
    {
        var data = await context.ActivitiesWeekly
            .Include(x => x.User)
                .ThenInclude(x => x.Department)
            .Include(x => x.User)
                .ThenInclude(x => x.UserCountry)
            .Include(x => x.User)
                .ThenInclude(x => x.LicenseLookups)
                    .ThenInclude(x => x.License)
            .ByFilter(filter)
            .ToListAsync();

        logger.LogInformation("Loaded {Count} records", data.Count);
        return data.Cast<IActivitiesWeeklyRecord>();
    }

    public async Task RefreshProfilingStats(int weeksToKeep)
    {
        if (weeksToKeep < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weeksToKeep), "Weeks to keep must be greater than 0");
        }
        logger.LogInformation("Refreshing profiling stats");
        await context.Database.ExecuteSqlRawAsync("EXEC [profiling].[usp_CompileWeekly] @WeeksToKeep = " + weeksToKeep);
    }
}

public static class SqlUsageDataLoaderFunc
{
    public static IQueryable<ActivitiesWeekly> ByFilter(this IQueryable<ActivitiesWeekly> source, LoaderUsageStatsReportFilter filter)
    {
        var dateFrom = DateOnly.FromDateTime(filter.From);
        var dateTo = DateOnly.FromDateTime(filter.To);
        return source
            .Where(d => d.MetricDate >= dateFrom && d.MetricDate <= dateTo)
            .ByDepartments(filter.InDepartments)
            .ByCountries(filter.InCountries);
    }

    static IQueryable<ActivitiesWeekly> ByDepartments(this IQueryable<ActivitiesWeekly> source, List<string> inList)
    {
        return source
            .Where(d => inList.Count == 0 ||
                (inList.Count > 0 && d.User.Department != null && inList.Contains(d.User.Department.Name)));
    }
    static IQueryable<ActivitiesWeekly> ByCountries(this IQueryable<ActivitiesWeekly> source, List<string> inList)
    {
        return source
            .Where(d => inList.Count == 0 ||
                (inList.Count > 0 && d.User.UserCountry != null && inList.Contains(d.User.UserCountry.Name)));
    }
}
