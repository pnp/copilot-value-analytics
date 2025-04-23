using Entities.DB.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities;

/// <summary>
/// User lookup for a session
/// </summary>
[Table("users")]
public class User : AbstractEFEntity, ITrackedUser
{
    [Column("upn")]
    public string UserPrincipalName { get; set; } = null!;

    [Column("mail")]
    public string? Mail { get; set; } = string.Empty;

    [Column("last_updated")]
    public DateTime? LastUpdated { get; set; } = null;

    [Column("message_not_before")]
    public DateTime? MessageNotBefore { get; set; } = null;

    [Column("azure_ad_id")]
    public string? AzureAdId { get; set; } = string.Empty;

    [Column("account_enabled")]
    public bool? AccountEnabled { get; set; }

    [MaxLength(50)]
    [Column("postalcode")]
    public string? PostalCode { get; set; } = string.Empty;

    public List<UserLicenseTypeLookup> LicenseLookups { get; set; } = new List<UserLicenseTypeLookup>();

    #region Lookup Properties

    [ForeignKey(nameof(CompanyName))]
    [Column("company_name_id")]
    public int? CompanyNameId { get; set; } = null;

    public CompanyName? CompanyName { get; set; } = null!;

    [ForeignKey(nameof(StateOrProvince))]
    [Column("state_or_province_id")]
    public int? StateOrProvinceId { get; set; } = null;

    public StateOrProvince? StateOrProvince { get; set; } = null!;


    [ForeignKey(nameof(Manager))]
    [Column("manager_id")]
    public int? ManagerId { get; set; } = null;

    public User? Manager { get; set; } = null!;

    [ForeignKey(nameof(UserCountry))]
    [Column("country_or_region_id")]
    public int? UserCountryId { get; set; } = null;

    public CountryOrRegion? UserCountry { get; set; } = null!;


    [ForeignKey(nameof(OfficeLocation))]
    [Column("office_location_id")]
    public int? OfficeLocationId { get; set; } = null;

    public UserOfficeLocation? OfficeLocation { get; set; } = null!;

    [ForeignKey(nameof(UsageLocation))]
    [Column("usage_location_id")]
    public int? UsageLocationId { get; set; } = null;

    public UserUsageLocation? UsageLocation { get; set; } = null!;


    [ForeignKey(nameof(Department))]
    [Column("department_id")]
    public int? DepartmentId { get; set; } = null;

    public UserDepartment? Department { get; set; } = null!;


    [ForeignKey(nameof(JobTitle))]
    [Column("job_title_id")]
    public int? JobTitleId { get; set; } = null;

    public UserJobTitle? JobTitle { get; set; } = null!;

    public List<string> Licenses => LicenseLookups.Select(l => l.License.Name).ToList();

    ITrackedUser? ITrackedUser.Manager => Manager;

    string? ITrackedUser.CompanyName => CompanyName?.Name;

    string? ITrackedUser.Department => Department?.Name;

    string? ITrackedUser.JobTitle => JobTitle?.Name;

    string? ITrackedUser.OfficeLocation => OfficeLocation?.Name;

    string? ITrackedUser.StateOrProvince => StateOrProvince?.Name;

    string? ITrackedUser.UsageLocation => UsageLocation?.Name;

    string? ITrackedUser.UserCountry => UserCountry?.Name;

    #endregion

    public override string ToString()
    {
        return $"{UserPrincipalName}";
    }
}

#region Lookup Tables

[Table("user_departments")]
public class UserDepartment : AbstractEFEntityWithName
{
}

[Table("user_country_or_region")]
public class CountryOrRegion : AbstractEFEntityWithName
{
}


[Table("user_job_titles")]
public class UserJobTitle : AbstractEFEntityWithName
{
}

[Table("user_office_locations")]
public class UserOfficeLocation : AbstractEFEntityWithName
{
}

[Table("user_usage_locations")]
public class UserUsageLocation : AbstractEFEntityWithName
{
}

[Table("user_state_or_province")]
public class StateOrProvince : AbstractEFEntityWithName
{
}

[Table("user_company_name")]
public class CompanyName : AbstractEFEntityWithName
{
}
#endregion
