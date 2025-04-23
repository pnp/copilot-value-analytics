using Azure.Core;
using Common.Engine.Config;
using Entities.DB.DbContexts;
using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace ActivityImporter.Engine.Graph.GraphUser;


/// <summary>
/// Ensures user table info is upto-date from Graph
/// </summary>
public class UserMetadataUpdater : AbstractApiLoader, IDisposable
{
    #region Constructor & Privates
    private readonly AppConfig _appConfig;
    private readonly ManualGraphCallClient _httpClient;
    private readonly GraphServiceClient _graphServiceClient;
    private readonly OfficeLicenseNameResolver _officeLicenseNameResolver;
    private readonly UserMetadataCache _userMetaCache;
    private readonly DataContext _db;
    private readonly GraphUserLoader _userLoader;

    public UserMetadataUpdater(AppConfig settings, ILogger telemetry, TokenCredential creds, ManualGraphCallClient manualGraphCallClient)
        : base(telemetry)
    {
        this._graphServiceClient = new GraphServiceClient(creds);
        this._officeLicenseNameResolver = new OfficeLicenseNameResolver();
        _appConfig = settings;


        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(_appConfig.ConnectionStrings.SQL);
        _db = new DataContext(optionsBuilder.Options);

        _userMetaCache = new UserMetadataCache(_db);

        // Override default
        _httpClient = manualGraphCallClient;
        _userLoader = new GraphUserLoader(settings.ConnectionStrings.Redis, settings.ImportAuthConfig.TenantId, _httpClient, _telemetry);
    }

    public GraphUserLoader GraphUserLoader => _userLoader;

    #endregion

    /// <summary>
    /// Main method
    /// </summary>
    public async Task InsertAndUpdateDatabaseUsersFromGraph()
    {
        _telemetry.LogInformation($"{DateTime.Now.ToShortTimeString()} User import - start");


        // If we have no active users, assume new install so clear delta key
        var activeUserCount = await _db.Users.Where(u => u.AccountEnabled.HasValue && u.AccountEnabled.Value == true).CountAsync();
        if (activeUserCount == 0)
        {
            await _userLoader.ClearUserQueryDeltaCode();
        }

        // Load from Graph & update delta code once done
        var allActiveGraphUsers = await _userLoader.LoadAllActiveUsers();

        // Get SKUs from tenant
        SubscribedSkuCollectionResponse? skus = null;
        try
        {
            skus = await _graphServiceClient.SubscribedSkus.GetAsync();
        }
        catch (ODataError ex)
        {
            if ((System.Net.HttpStatusCode)ex.ResponseStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _telemetry.LogWarning($"User import - couldn't load SKUs for org - {ex.Message}. Ensure 'Organization.Read.All' in granted.");
            }
            else
            {
                _telemetry.LogError(ex, $"User import - couldn't load SKUs for org - {ex.Message}");
            }

            // If we can't get tenant SKUs to find all users by, we can get SKUs per user instead, but this can be very slow.
            _telemetry.LogWarning($"User import - will load SKUs directly from each user instead. This will be slow.");
        }

        var allDbUsers = await _db.Users.Include(u => u.LicenseLookups).ToListAsync();
        var graphMentionedExistingDbUsers = GetDbUsersFromGraphUsers(allActiveGraphUsers, allDbUsers);

        // Insert any user we've not seen so far
        var insertedDbUsers = await InsertMissingUsers(_db, allActiveGraphUsers, graphMentionedExistingDbUsers, skus == null);
        var notInserted = allActiveGraphUsers.Where(
            u => !string.IsNullOrEmpty(u.UserPrincipalName) &&
                !insertedDbUsers.Where(i => i.UserPrincipalName.ToLower() == u.UserPrincipalName.ToLower()).Any()).ToList();


        // Check existing users again Graph updates
        _telemetry.LogInformation($"User import - updating {notInserted.Count.ToString("N0")} existing users...");
        foreach (var existingGraphUser in notInserted)
        {
            var dbUser = graphMentionedExistingDbUsers.Where(u => u.UserPrincipalName.ToLower() == existingGraphUser.UserPrincipalName.ToLower()).SingleOrDefault();
            if (dbUser == null)
            {
                _telemetry.LogDebug($"User import - couldn't find existing user '{existingGraphUser.UserPrincipalName}' in DB");
                continue;
            }

            await UpdateDbUserWithGraphData(_db, existingGraphUser, allActiveGraphUsers, allDbUsers, dbUser, skus == null);
        }

        // Combine inserted & modified _db users
        var allProcessedDbUsers = new List<Entities.DB.Entities.User>(insertedDbUsers);
        var notInsertDbUsers = GetDbUsersFromGraphUsers(notInserted, graphMentionedExistingDbUsers);
        allProcessedDbUsers.AddRange(notInsertDbUsers);

        // Can we update SKUs for users on batch (ie Organization.Read.All granted)?
        if (skus?.Value != null)
        {
            await ProcessSKUsForAllUsers(skus.Value, allProcessedDbUsers);
            _telemetry.LogInformation($"User import - updated user license information from {skus.Value.Count.ToString("N0")} tenant SKUs");
        }

        _db.ChangeTracker.DetectChanges();
        await _db.SaveChangesAsync();
        _telemetry.LogInformation($"{DateTime.Now.ToShortTimeString()} User import - inserted {insertedDbUsers.Count.ToString("N0")} new users and updated {notInserted.Count.ToString("N0")} from Graph API");

    }

    private async Task ProcessSKUsForAllUsers(List<SubscribedSku> skus, List<Entities.DB.Entities.User> graphFoundDbUsers)
    {
        // Remove all license info from all users 1st
        _db.UserLicenseTypeLookups.RemoveRange(graphFoundDbUsers.SelectMany(u => u.LicenseLookups));

        foreach (var sku in skus)
        {
            var usersWithSku = await _graphServiceClient.Users.GetAsync(o =>
            {
                o.QueryParameters.Select = ["userPrincipalName"];
                o.QueryParameters.Filter = $"assignedLicenses/any(u:u/skuId eq {sku.SkuId})";
            });
            if (usersWithSku != null)
            {

                var allUsersWithSku = new List<Microsoft.Graph.Models.User>();

                int skuPage = 1;


                // Recursively load users 
                var pageIterator = PageIterator<Microsoft.Graph.Models.User, UserCollectionResponse>
                .CreatePageIterator(
                    _graphServiceClient,
                    usersWithSku,
                    // Callback executed for each item in
                    // the collection
                    (userWithSku) =>
                    {
                        allUsersWithSku.Add(userWithSku);
                        skuPage++;

#if DEBUG
                        Console.WriteLine($"DEBUG: SKU {sku.SkuPartNumber} page {skuPage}");
#endif
                        return true;
                    });


                // Update all
                await AddSkuForUsers(graphFoundDbUsers, allUsersWithSku, sku);
            }

        }

    }

    private async Task AddSkuForUsers(List<Entities.DB.Entities.User> graphFoundDbUsers, List<Microsoft.Graph.Models.User> usersWithSku,
        SubscribedSku sku)
    {
        if (sku.SkuPartNumber == null)
        {
            _telemetry.LogWarning($"User import - SKU with no part-number found. Skipping.");
            return;
        }
        var relevantDbUsers = new List<Entities.DB.Entities.User>();
        foreach (var graphUser in usersWithSku)
            foreach (var dbUser in graphFoundDbUsers)
                if (graphUser.UserPrincipalName?.ToLower() == dbUser.UserPrincipalName.ToLower())
                {
                    relevantDbUsers.Add(dbUser);
                    break;
                }

        _telemetry.LogInformation($"Found {relevantDbUsers.Count.ToString("N0")} users in SQL for SKU Part Number '{sku.SkuPartNumber}' from {usersWithSku.Count.ToString("N0")} Graph users.");

        var list = new List<UserLicenseTypeLookup>();
        int i = 0;
        foreach (var dbUser in relevantDbUsers)
        {
            var licence = await GetLicenseType(sku.SkuPartNumber);
            list.Add(new UserLicenseTypeLookup { License = licence, User = dbUser });

            if (i > 0 && i % 1000 == 0)
            {
                Console.WriteLine($"User {i.ToString("N0")} / {relevantDbUsers.Count.ToString("N0")} processed for licenses.");
            }
        }
        _db.UserLicenseTypeLookups.AddRange(list);
    }

    private async Task UpdateDbUserWithGraphData(DataContext _db, GraphUser graphUser, List<GraphUser> allGraphUsers, List<Entities.DB.Entities.User> allDbUsers, Entities.DB.Entities.User dbUser, bool readUserSkus)
    {
        UpdateDbUserFromGraphUser(dbUser, graphUser);

        var nameMaxLengthDepartment = StringUtils.EnsureMaxLength(graphUser.Department?.Trim(), 100);
        dbUser.Department = !string.IsNullOrEmpty(nameMaxLengthDepartment) ?
            await _userMetaCache.DepartmentCache.GetOrCreateNewResource(nameMaxLengthDepartment,
                new UserDepartment { Name = nameMaxLengthDepartment }) : null;

        var nameMaxLengthJobTitle = StringUtils.EnsureMaxLength(graphUser.JobTitle?.Trim(), 100);
        dbUser.JobTitle = !string.IsNullOrEmpty(nameMaxLengthJobTitle) ?
            await _userMetaCache.JobTitleCache.GetOrCreateNewResource(nameMaxLengthJobTitle,
                new UserJobTitle { Name = nameMaxLengthJobTitle }) : null;

        var nameMaxLengthOfficeLocation = StringUtils.EnsureMaxLength(graphUser.OfficeLocation?.Trim(), 100);
        dbUser.OfficeLocation = !string.IsNullOrEmpty(nameMaxLengthOfficeLocation) ?
            await _userMetaCache.OfficeLocationCache.GetOrCreateNewResource(nameMaxLengthOfficeLocation,
                new UserOfficeLocation { Name = nameMaxLengthOfficeLocation }) : null;

        var nameMaxLengthUsageLocation = StringUtils.EnsureMaxLength(graphUser.UsageLocation?.Trim(), 100);
        dbUser.UsageLocation = !string.IsNullOrEmpty(nameMaxLengthUsageLocation) ?
            await _userMetaCache.UseageLocationCache.GetOrCreateNewResource(nameMaxLengthUsageLocation,
                new UserUsageLocation { Name = nameMaxLengthUsageLocation }) : null;

        var nameMaxLengthCountry = StringUtils.EnsureMaxLength(graphUser.Country?.Trim(), 100);
        dbUser.UserCountry = !string.IsNullOrEmpty(nameMaxLengthCountry) ?
            await _userMetaCache.CountryOrRegionCache.GetOrCreateNewResource(nameMaxLengthCountry,
                new CountryOrRegion { Name = nameMaxLengthCountry }) : null;

        var nameMaxLengthState = StringUtils.EnsureMaxLength(graphUser.State?.Trim(), 100);
        dbUser.StateOrProvince = !string.IsNullOrEmpty(nameMaxLengthState) ?
            await _userMetaCache.StateOrProvinceCache.GetOrCreateNewResource(nameMaxLengthState,
                new StateOrProvince { Name = nameMaxLengthState }) : null;

        var nameMaxLengthCompany = StringUtils.EnsureMaxLength(graphUser.CompanyName?.Trim(), 100);
        dbUser.CompanyName = !string.IsNullOrEmpty(nameMaxLengthCompany) ?
            await _userMetaCache.CompanyNameCache.GetOrCreateNewResource(nameMaxLengthCompany,
                new CompanyName { Name = nameMaxLengthCompany }) : null;

        if (graphUser.DefaultManagerInfo?.Id != null)
        {
            // Try getting manager from DB 1st
            var dbManager = allDbUsers.Where(u => !string.IsNullOrEmpty(u.AzureAdId) && new Guid(u.AzureAdId).Equals(new Guid(graphUser.DefaultManagerInfo.Id))).FirstOrDefault();
            if (dbManager == null)
            {
                var graphManagerUser = allGraphUsers.FirstOrDefault(u => u.Id == graphUser.DefaultManagerInfo?.Id);

                if (graphManagerUser != null)
                {
                    // Got user from Graph cache; get DB user by UPN
                    var managerUpn = graphManagerUser.UserPrincipalName?.ToLower();

                    dbUser.Manager = !string.IsNullOrEmpty(managerUpn) ?
                        await _userMetaCache.UserCache.GetOrCreateNewResource(managerUpn,
                            new Entities.DB.Entities.User { UserPrincipalName = managerUpn }, true) : null;

                }

                else
                {
                    _telemetry.LogWarning($"Couldn't find manager with AAD ID {graphUser.DefaultManagerInfo?.Id} in Graph cache or DB");
                }
            }
            else
            {
                dbUser.Manager = dbManager;
            }
        }
        dbUser.LastUpdated = DateTime.Now;

        // This is only done per user if can't be done at tenant level (due to extra permission)
        if (readUserSkus)
        {
            // Get user service-plan from Graph
            // Service plan names - https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference
            LicenseDetailsCollectionResponse? userServicePlans = null;
            try
            {
                userServicePlans = await _graphServiceClient.Users[graphUser.Id].LicenseDetails
                    .GetAsync(op => op.QueryParameters.Select = ["skuPartNumber", "skuId"]);
            }
            catch (ServiceException ex)
            {
                _telemetry.LogError(ex, $"User import - couldn't load service-plans for user ID '{graphUser.Id}' - {ex.Message}");
            }

            if (userServicePlans?.Value != null)
            {
                var allLicenses = new List<LicenseType>();
                foreach (var userPlan in userServicePlans.Value)
                {
                    if (userPlan.SkuPartNumber != null)
                        allLicenses.Add(await GetLicenseType(userPlan.SkuPartNumber));
                }

                // Remove old lookups (simpler) & re-add
                _db.UserLicenseTypeLookups.RemoveRange(dbUser.LicenseLookups.Where(l => l.IsSavedToDB));
                foreach (var licence in allLicenses)
                {
                    dbUser.LicenseLookups.Add(new UserLicenseTypeLookup { License = licence, User = dbUser });
                }
            }
        }
    }

    private async Task<LicenseType> GetLicenseType(string skuPartNumber)
    {
        var productName = _officeLicenseNameResolver.GetDisplayNameFor(skuPartNumber);
        if (string.IsNullOrEmpty(productName))
        {
            _telemetry.LogWarning($"User import - unexpected SKU part-number '{skuPartNumber}'. Couldn't find a corresponding display-name.");

            // Set display name as SKU ID
            productName = skuPartNumber;
        }

        var thisLicense = await _userMetaCache.LicenseTypeCache.GetOrCreateNewResource(productName,
            new LicenseType
            {
                Name = productName,
                SKUID = skuPartNumber
            });
        return thisLicense;
    }


    private List<Entities.DB.Entities.User> GetDbUsersFromGraphUsers(List<GraphUser> allGraphUsers, List<Entities.DB.Entities.User> allDbUsers)
    {
        var users = new List<Entities.DB.Entities.User>();

        foreach (var graphUser in allGraphUsers)
        {
            // Do we have this graph user?
            var upn = graphUser.UserPrincipalName?.ToLower();
            if (!string.IsNullOrEmpty(upn))
            {
                var dbUser = allDbUsers.Where(u => u.UserPrincipalName.ToLower() == upn).SingleOrDefault();
                if (dbUser != null)
                {
                    users.Add(dbUser);
                }
            }
        }

        return users;
    }

    /// <summary>
    /// Inserts missing users into DB & calls UpdateDbUserWithGraphData
    /// </summary>
    private async Task<List<Entities.DB.Entities.User>> InsertMissingUsers(DataContext _db, List<GraphUser> allGraphUsers, List<Entities.DB.Entities.User> graphMentionedDbUsers, bool readUserSkus)
    {
        _telemetry.LogInformation($"User import - Inserting missing users...");
        var usersInserted = new List<Entities.DB.Entities.User>();

        // Build list of users to insert
        foreach (var graphUser in allGraphUsers)
        {
            // Do we have this graph user?
            var upn = graphUser.UserPrincipalName?.ToLower();
            if (!string.IsNullOrEmpty(upn) && !graphMentionedDbUsers.Where(u => u.UserPrincipalName.ToLower() == upn).Any())
            {
                // Lookup manager will just add to cache but not to context
                var dbUser = await _userMetaCache.UserCache.GetOrCreateNewResource(upn, UpdateDbUserFromGraphUser(new Entities.DB.Entities.User { UserPrincipalName = upn }, graphUser));
                usersInserted.Add(dbUser);
            }
        }

        // Update too each user. 
        int i = 0;
        _telemetry.LogInformation($"User import - Loading metadata for {usersInserted.Count.ToString("N0")} new users...");

        foreach (var newDbUser in usersInserted)
        {
            var graphUser = allGraphUsers.Where(u => u.UserPrincipalName.ToLower() == newDbUser.UserPrincipalName).FirstOrDefault();
            if (graphUser == null)
            {
                _telemetry.LogWarning($"User import - couldn't find Graph user for new user '{newDbUser.UserPrincipalName}'");
                continue;
            }
            await UpdateDbUserWithGraphData(_db, graphUser, allGraphUsers, graphMentionedDbUsers, newDbUser, readUserSkus);

            if (i > 0 && i % 1000 == 0)
            {
                Console.WriteLine($"New user {i}/{usersInserted.Count.ToString("N0")} processed for lookups.");
            }
            i++;
        }

        _db.Users.AddRange(usersInserted);

        Console.WriteLine($"User import - Saving {usersInserted.Count.ToString("N0")} new users to SQL...");
        await _db.SaveChangesAsync();

        return usersInserted;
    }

    private Entities.DB.Entities.User UpdateDbUserFromGraphUser(Entities.DB.Entities.User dbUser, GraphUser graphUser)
    {
        dbUser.AccountEnabled = graphUser.AccountEnabled;
        dbUser.PostalCode = graphUser.PostalCode;
        dbUser.AzureAdId = graphUser.Id;
        dbUser.Mail = graphUser.Mail;

        return dbUser;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}