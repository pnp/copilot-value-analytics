using Entities.DB.Entities.AuditLog;
using Entities.DB.Entities.SP;
using Entities.DB.Entities.UsageReports;
using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace Entities.DB.DbContexts;


public class DataContext : CommonContext
{
    public DbSet<IgnoredEvent> IgnoredAuditEvents { get; set; }

    public DbSet<SharePointEventMetadata> SharePointEvents { get; set; }

    public DbSet<ImportSiteFilter> ImportSiteFilters { get; set; }
    public DbSet<CommonAuditEvent> AuditEventsCommon { get; set; }

    public DbSet<UserUsageLocation> UserUsageLocations { get; set; }

    public DbSet<SPEventFileExtension> SharePointFileExtensions { get; set; }
    public DbSet<SPEventFileName> SharePointFileNames { get; set; }
    public DbSet<EventOperation> EventOperations { get; set; }
    public DbSet<SPEventType> SharePointEventType { get; set; }

    public DbSet<Site> Sites { get; set; }
    public DbSet<Url> Urls { get; set; }
    public DbSet<OnlineMeeting> OnlineMeetings { get; set; }

    public DbSet<GlobalTeamsUserUsageLog> TeamUserActivityLogs { get; set; }
    public DbSet<GlobalTeamsUserDeviceUsageLog> TeamsUserDeviceUsageLog { get; set; }
    public DbSet<YammerUserActivityLog> YammerUserActivityLogs { get; set; }
    public DbSet<YammerDeviceActivityLog> YammerDeviceActivityLogs { get; set; }

    public DbSet<AppPlatformUserActivityLog> AppPlatformUserUsageLog { get; set; }

    public DbSet<OutlookUsageActivityLog> OutlookUsageActivityLogs { get; set; }
    public DbSet<OneDriveUserActivityLog> OneDriveUserActivityLogs { get; set; }
    public DbSet<SharePointUserActivityLog> SharePointUserActivityLogs { get; set; }

    public DbSet<CopilotChat> CopilotChats { get; set; }
    public DbSet<CopilotEventMetadataFile> CopilotEventMetadataFiles { get; set; }
    public DbSet<CopilotEventMetadataMeeting> CopilotEventMetadataMeetings { get; set; }

    public DbSet<UserSurveyResponseActivityType> SurveyResponseActivities { get; set; }

    public DbSet<UserSurveyResponseActivityType> SurveyResponseActivityTypes { get; set; }
    public DbSet<CopilotActivity> CopilotActivities { get; set; }
    public DbSet<CopilotAgentType> CopilotAgentTypes { get; set; }
    public DbSet<CopilotActivityType> CopilotActivityTypes { get; set; }


    public DbSet<SurveyPageDB> SurveyPages { get; set; }
    public DbSet<SurveyQuestionDefinitionDB> SurveyQuestionDefinitions { get; set; }
    public DbSet<SurveyQuestionResponseDB> SurveyQuestionResponses { get; set; }
    public DbSet<SurveyGeneralResponseDB> SurveyGeneralResponses { get; set; }


    /// <summary>
    /// Define model schema
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Add unique indexes
        modelBuilder.Entity<Site>().HasIndex(b => b.UrlBase).IsUnique();

        modelBuilder.Entity<Url>()
         .HasIndex(t => new { t.FullUrl })
         .IsUnique();

        modelBuilder.Entity<SPEventFileName>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<SPEventType>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<SPEventFileExtension>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<EventOperation>()
         .HasIndex(t => new { t.Name })
         .IsUnique();

        modelBuilder.Entity<UserDepartment>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<UserJobTitle>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<UserOfficeLocation>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<StateOrProvince>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<CountryOrRegion>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<CompanyName>()
         .HasIndex(t => new { t.Name })
         .IsUnique();


        modelBuilder.Entity<User>()
         .HasIndex(t => new { t.UserPrincipalName })
         .IsUnique();


        modelBuilder.Entity<UserLicenseTypeLookup>()
         .HasIndex(t => new { t.LicenseTypeId, t.UserId })
         .IsUnique();

        modelBuilder.Entity<SurveyQuestionDefinitionDB>()
         .HasIndex(t => new { t.QuestionId })
         .IsUnique();


        modelBuilder.Entity<CopilotActivityType>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<CopilotActivity>()
         .HasIndex(t => new { t.Name })
         .IsUnique();

        modelBuilder.Entity<ImportSiteFilter>()
         .HasIndex(t => t.UrlBase)
         .IsUnique();

        modelBuilder.Entity<CopilotAgentType>()
         .HasIndex(t => t.AgentID)
         .IsUnique();


        modelBuilder.Entity<SurveyQuestionDefinitionDB>()
         .HasIndex(t => t.QuestionText)
         .IsUnique();

        modelBuilder.Entity<SurveyGeneralResponseDB>()
         .HasOne(f => f.User)
         .WithMany()
         .OnDelete(DeleteBehavior.Restrict);

        base.OnModelCreating(modelBuilder);
    }


    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }
    public async Task<bool> EnsureCreated()
    {
        return await Database.EnsureCreatedAsync();
    }
}

