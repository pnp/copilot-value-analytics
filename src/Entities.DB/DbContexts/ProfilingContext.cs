using Entities.DB.Entities;
using Entities.DB.Entities.Profiling;
using Microsoft.EntityFrameworkCore;

namespace Entities.DB.DbContexts;


public class ProfilingContext : CommonContext
{
    public ProfilingContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<ActivitiesWeekly> ActivitiesWeekly { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivitiesWeekly>()
            .ToTable("ActivitiesWeekly", "profiling")
            .HasKey(a => new { a.Metric, a.MetricDate, a.UserID });

        modelBuilder.Entity<User>().ToTable("users", "dbo");
        modelBuilder.Entity<UserDepartment>().ToTable("user_departments", "dbo");
        modelBuilder.Entity<StateOrProvince>().ToTable("state_or_provinces", "dbo");
        modelBuilder.Entity<CountryOrRegion>().ToTable("user_country_or_region", "dbo");
        modelBuilder.Entity<CompanyName>().ToTable("company_names", "dbo");
        modelBuilder.Entity<UserOfficeLocation>().ToTable("user_office_locations", "dbo");
        modelBuilder.Entity<UserJobTitle>().ToTable("user_job_titles", "dbo");
        modelBuilder.Entity<UserLicenseTypeLookup>().ToTable("user_license_type_lookups", "dbo");
        modelBuilder.Entity<LicenseType>().ToTable("license_types", "dbo");

        modelBuilder.HasDefaultSchema("profiling");
    }
}
