using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace Entities.DB.DbContexts;

public abstract class CommonContext : DbContext
{
    public CommonContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<UserDepartment> UserDepartments { get; set; }

    public DbSet<StateOrProvince> StateOrProvinces { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<CountryOrRegion> CountryOrRegions { get; set; }
    public DbSet<CompanyName> CompanyNames { get; set; }
    public DbSet<UserOfficeLocation> UserOfficeLocations { get; set; }
    public DbSet<UserJobTitle> UserJobTitles { get; set; }
    public DbSet<LicenseType> LicenseTypes { get; set; }
    public DbSet<UserLicenseTypeLookup> UserLicenseTypeLookups { get; set; }
}
