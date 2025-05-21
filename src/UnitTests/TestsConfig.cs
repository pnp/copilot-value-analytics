using Common.DataUtils.Config;
using Common.Engine.Config;
using Microsoft.Extensions.Configuration;

namespace UnitTests;

public class TestsConfig : AppConfig
{
    public TestsConfig(IConfiguration config) : base(config)
    {
    }

    /// <summary>
    /// Example: https://m365x35901285-my.sharepoint.com/personal/admin_m365x35901285_onmicrosoft_com/Documents/Employee%20Engagement%20Plan.docx
    /// </summary>
    [ConfigValue]
    public string TestCopilotDocContextIdMySites { get; set; } = null!;


    /// <summary>
    /// Example: https://m365cp123890.sharepoint.com/sites/ProjectFalcon-InternalTeam/Shared%20Documents/Reports/Microsoft_Work_Trend_Index_Special_Report_2022_Full_Report.docx
    /// </summary>
    [ConfigValue]
    public string TestCopilotDocContextIdSpSite { get; set; } = null!;

    [ConfigValue]
    public string TestCopilotEventUPN { get; set; } = null!;


    [ConfigValue]
    public string MySitesFileUrl { get; set; } = null!;
    [ConfigValue]
    public string MySitesFileName { get; set; } = null!;

    [ConfigValue]
    public string MySitesFileExtension { get; set; } = null!;



    [ConfigValue]
    public string TeamSiteFileUrl { get; set; } = null!;
    [ConfigValue]
    public string TeamSitesFileName { get; set; } = null!;

    [ConfigValue]
    public string TeamSiteFileExtension { get; set; } = null!;


    [ConfigValue(true)]
    public string TestCallThreadId { get; set; } = null!;
}
