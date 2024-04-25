﻿using Entities.DB.Entities;
using Entities.DB.Entities.AuditLog;
using Entities.DB.Entities.SP;
using Entities.DB.Entities.UsageReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Entities.DB;

public class DataContext : DbContext
{
    #region Props

    public DbSet<User> Users { get; set; }
    public DbSet<IgnoredEvent> IgnoredAuditEvents { get; set; }

    public DbSet<SharePointEventMetadata> SharePointEvents { get; set; }

    public DbSet<ImportSiteFilter> ImportSiteFilters { get; set; }
    public DbSet<CommonAuditEvent> AuditEventsCommon { get; set; }

    public DbSet<UserUsageLocation> UserUsageLocations { get; set; }

    public DbSet<SPEventFileExtension> SharePointFileExtensions { get; set; }
    public DbSet<SPEventFileName> SharePointFileNames { get; set; }
    public DbSet<EventOperation> EventOperations { get; set; }
    public DbSet<SPEventType> SharePointEventType { get; set; }

    public DbSet<UserJobTitle> UserJobTitles { get; set; }
    public DbSet<Site> Sites { get; set; }
    public DbSet<Url> Urls { get; set; }
    public DbSet<UserDepartment> UserDepartments { get; set; }

    public DbSet<OnlineMeeting> OnlineMeetings { get; set; }

    public DbSet<CountryOrRegion> CountryOrRegions { get; set; }
    public DbSet<CompanyName> CompanyNames { get; set; }
    public DbSet<UserOfficeLocation> UserOfficeLocations { get; set; }
    public DbSet<GlobalTeamsUserUsageLog> TeamUserActivityLogs { get; set; }

    public DbSet<OutlookUsageActivityLog> OutlookUsageActivityLogs { get; set; }
    public DbSet<OneDriveUserActivityLog> OneDriveUserActivityLogs { get; set; }
    public DbSet<SharePointUserActivityLog> SharePointUserActivityLogs { get; set; }

    public DbSet<CopilotChat> CopilotChats { get; set; }
    public DbSet<CopilotEventMetadataFile> CopilotEventMetadataFiles { get; set; }
    public DbSet<CopilotEventMetadataMeeting> CopilotEventMetadataMeetings { get; set; }


    public DbSet<UserSurveyResponse> SurveyResponses { get; set; }
    public DbSet<UserSurveyResponseActivityType> SurveyResponseActivities { get; set; }

    public DbSet<UserSurveyResponseActivityType> SurveyResponseActivityTypes { get; set; }
    public DbSet<CopilotActivity> CopilotActivities { get; set; }
    public DbSet<CopilotActivityType> CopilotActivityTypes { get; set; }

    #endregion

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
         .HasIndex(t => new { t.type_name })
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


        modelBuilder.Entity<CopilotActivityType>()
         .HasIndex(t => new { t.Name })
         .IsUnique();
        modelBuilder.Entity<CopilotActivity>()
         .HasIndex(t => new { t.Name })
         .IsUnique();


        // Removed due to issues inserting fake data
        //modelBuilder.Entity<UserSurveyResponse>()
        // .HasIndex(t => new { t.RelatedEventId, t.UserID })
        // .IsUnique();

        modelBuilder.Entity<UserSurveyResponse>()
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


public class ServiceSqlDbContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CopilotFeedbackDev;Trusted_Connection=True;MultipleActiveResultSets=true");

        return new DataContext(optionsBuilder.Options);
    }
}
