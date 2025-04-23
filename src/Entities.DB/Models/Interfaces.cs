namespace Entities.DB.Models;
public interface IActivitiesWeeklyRecord
{
    string Metric { get; }
    DateOnly MetricDate { get; }
    int Sum { get; set; }
    ITrackedUser User { get; }
}


public interface ITrackedUser
{
    string? CompanyName { get; }
    string? Department { get; }
    string? JobTitle { get; }
    string? OfficeLocation { get; }
    string? StateOrProvince { get; }
    string? UsageLocation { get; }
    string? UserCountry { get; }
    List<string> Licenses { get; }
    ITrackedUser? Manager { get; }
    string UserPrincipalName { get; }
}

public interface ILookupEntity
{
    string Name { get; }
}
