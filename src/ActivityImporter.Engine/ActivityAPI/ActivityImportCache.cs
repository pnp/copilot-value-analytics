using ActivityImporter.Engine.ActivityAPI.Models;
using Entities.DB.DbContexts;
using Entities.DB.Entities.AuditLog;
using Entities.DB.Entities.SP;
using Microsoft.EntityFrameworkCore;

namespace ActivityImporter.Engine.ActivityAPI;

/// <summary>
/// List of activity reports that are cached either because they're processed or newly ignored. Processed includes previously ignored. 
/// Cache is chunked into items per hour to speed up indexing?
/// </summary>
public class ActivityImportCache
{
    private ActivityImportCache()
    {
        ProcessedIDs = new Dictionary<int, Dictionary<Guid, DateTime>>();
        NewlyIgnoredIDs = new Dictionary<int, Dictionary<Guid, DateTime>>();
        AnonymisedUserNameCache = new Dictionary<string, string>();
    }

    public static ActivityImportCache GetEmptyCache()
    {
        return new ActivityImportCache();
    }

    public static ActivityImportCache GetAndBuildNewCache(DataContext db, DateTime cacheFrom, DateTime cacheTo)
    {
        return GetAndBuildNewCache(cacheFrom, cacheTo, db);

    }

    /// <summary>
    /// Get new import-cache, with processed & ignored IDs already added for a specific range
    /// </summary>
    public static ActivityImportCache GetAndBuildNewCache(DateTime cacheFrom, DateTime cacheTo, DataContext db)
    {
        if (cacheFrom > cacheTo)
        {
            throw new ArgumentOutOfRangeException("'From' date-time should be greater than 'to' clause.");
        }

        // Include an extra minute either side of cache-loading range, as EF6 assumes datetime2 which can miss some datetime edge values
        // This is easier than doing a new migration to convert every DT field.
        cacheTo = cacheTo.AddMinutes(1);
        cacheFrom = cacheFrom.AddDays(-1);

#if DEBUG
        Console.WriteLine($"DEBUG: Loading activity cache from {cacheFrom} to {cacheTo}.");
#endif

        var c = new ActivityImportCache();

        // Build list of events to ignore
        // Get events from X time before
        var ignoredEvents = db.IgnoredAuditEvents.Where(e => e.Processed >= cacheFrom && e.Processed <= cacheTo).ToList();

        var importedEventsQuery = db.AuditEventsCommon.Where(e => e.TimeStamp >= cacheFrom && e.TimeStamp <= cacheTo)
            .Select(e => new BasicEventDetails() { ID = e.Id, Time = e.TimeStamp });
        var importedEvents = importedEventsQuery.ToList();


        // Processed means "ignored or saved"
        foreach (var ignoredEvent in ignoredEvents)
        {
            c.AddProcessedID(ignoredEvent.AuditEventId, ignoredEvent.Processed);
        }
        foreach (var e in importedEvents)
        {
            c.AddProcessedID(e.ID, e.Time);
        }


        return c;
    }

    // Just used above
    private class BasicEventDetails
    {
        public DateTime Time { get; set; }
        public Guid ID { get; set; }
    }

    public enum CacheType
    {
        None,
        NewlyIgnored,
        Processed
    }

    /// <summary>
    /// Return the cache dictionary for a specific DT
    /// </summary>
    Dictionary<Guid, DateTime> GetCacheChunkForAuditLog(DateTime dt, CacheType cacheType)
    {
        if (cacheType == CacheType.None)
        {
            throw new ArgumentOutOfRangeException("cacheType");
        }

        TimeSpan span = dt.Subtract(DateTime.MinValue);
        int key = (int)Math.Round(span.TotalHours, 0);
        Dictionary<int, Dictionary<Guid, DateTime>>? targetDictionary = null;

        // Pick type of dictionary
        if (cacheType == CacheType.NewlyIgnored)
        {
            targetDictionary = NewlyIgnoredIDs;
        }
        else if (cacheType == CacheType.Processed)
        {
            targetDictionary = ProcessedIDs;
        }

        // Pick cache-chunk by hour
        if (targetDictionary == null)
        {
            throw new Exception("Unexpected null targetDictionary");
        }
        if (!targetDictionary.ContainsKey(key))
        {
            targetDictionary.Add(key, new Dictionary<Guid, DateTime>());
        }
        return targetDictionary[key];
    }

    /// <summary>
    /// Return the cache dictionary for a specific DT
    /// </summary>
    Dictionary<Guid, DateTime> GetCacheChunkForAuditLog(AbstractAuditLogContent log, CacheType type)
    {
        return GetCacheChunkForAuditLog(log.CreationTime, type);
    }

    void AddProcessedID(Guid id, DateTime processedDate)
    {
        lock (this)
        {
            GetCacheChunkForAuditLog(processedDate, CacheType.Processed).Add(id, processedDate);
        }
    }


    /// <summary>
    /// Cached list of all processed event IDs
    /// </summary>
    private Dictionary<int, Dictionary<Guid, DateTime>> ProcessedIDs { get; set; }

    /// <summary>
    /// List of all event IDs ignored (not imported). Events also added to ProcessedIDs.
    /// </summary>
    private Dictionary<int, Dictionary<Guid, DateTime>> NewlyIgnoredIDs { get; set; }

    private List<ImportSiteFilter>? _orgUrlCache = null;
    public List<ImportSiteFilter> OrgUrlCache(DataContext db)
    {
        if (_orgUrlCache == null)
        {
            _orgUrlCache = db.ImportSiteFilters.ToList();

        }
        return _orgUrlCache;
    }

    /// <summary>
    /// Real username + hash
    /// </summary>
    public Dictionary<string, string> AnonymisedUserNameCache { get; set; }

    public List<Dictionary<Guid, DateTime>> GetIds(CacheType cacheType)
    {
        if (cacheType == CacheType.NewlyIgnored)
        {
            return NewlyIgnoredIDs.Values.ToList();
        }
        else if (cacheType == CacheType.Processed)
        {
            return ProcessedIDs.Values.ToList();
        }
        throw new ArgumentOutOfRangeException("cacheType");
    }

    /// <summary>
    /// Clear "ignored" cache
    /// </summary>
    public void ClearNewIgnoredIDs()
    {
        lock (this)
        {
            NewlyIgnoredIDs.Clear();

        }
    }

    /// <summary>
    /// Checks the "processed" and ignored cache.
    /// </summary>
    public bool HaveSeenInProcessedOrIgnoredEvents(AbstractAuditLogContent auditLogContent)
    {
        return HaveSeenInProcessedOrIgnoredEvents(auditLogContent.Id);
    }

    public bool HaveSeenInProcessedOrIgnoredEvents(Guid id)
    {
        lock (this)
        {

            foreach (Dictionary<Guid, DateTime> cache in ProcessedIDs.Values)
            {
                if (cache.ContainsKey(id))
                {
                    return true;
                }
            }

            foreach (Dictionary<Guid, DateTime> cache in NewlyIgnoredIDs.Values)
            {
                if (cache.ContainsKey(id))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Add item to "processed" queue
    /// </summary>
    public void RememberProcessedEvent(AbstractAuditLogContent auditLogContent)
    {
        lock (this)
        {
            Dictionary<Guid, DateTime> theCache = GetCacheChunkForAuditLog(auditLogContent, CacheType.Processed);
            theCache.Add(auditLogContent.Id, auditLogContent.CreationTime);
        }
    }

    /// <summary>
    /// Remember a new event to ignore. Will be saved in DownloadChunk.DownloadAllAndSaveToSQL
    /// </summary>
    internal void RememberNewlyIgnoredEvent(AbstractAuditLogContent auditLogContent)
    {
        lock (this)
        {
            GetCacheChunkForAuditLog(auditLogContent, CacheType.NewlyIgnored).Add(auditLogContent.Id, auditLogContent.CreationTime);
            GetCacheChunkForAuditLog(auditLogContent, CacheType.Processed).Add(auditLogContent.Id, auditLogContent.CreationTime);
        }
    }

    // Sync for ProcessedIDs
    private static object _idCheckLock = new object();


    internal async Task SaveNewlyIgnoredEvents(DataContext db)
    {
        // Update ignored
        List<IgnoredEvent> ignoreList = new List<IgnoredEvent>();

        // Prevent any loader threads from adding to "ignored events" cache
        lock (_idCheckLock)
        {
            // Save newly ignored events to table in SQL
            foreach (var cache in GetIds(CacheType.NewlyIgnored))
            {
                foreach (var newIgnoredEvent in cache)
                {
                    ignoreList.Add(new IgnoredEvent() { AuditEventId = newIgnoredEvent.Key, Processed = newIgnoredEvent.Value });
                }
            }
            // Events processes also added to ProcessedEvents. 
            ClearNewIgnoredIDs();
        }
        db.IgnoredAuditEvents.AddRange(ignoreList);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            const string PRIMARY_KEY_VIOLATION = "Violation of PRIMARY KEY constraint 'PK_processed_audit_events'. Cannot insert duplicate key in object 'dbo.ignored_audit_events'";
            if (CommonExceptionHandler.GetErrorText(ex).Contains(PRIMARY_KEY_VIOLATION))
            {
                await DeleteIgnoredEvents(ignoreList, db);
            }
        }
        Console.WriteLine($"Saved {ignoreList.Count.ToString("n0")} events to ignore-list.");
    }



    private async Task DeleteIgnoredEvents(List<IgnoredEvent> ignoreList, DataContext db)
    {
        var ignoreGuids = ignoreList.Select(e => e.AuditEventId).ToList();
        var ignoreEventRecords = db.IgnoredAuditEvents.Where(e => ignoreGuids.Contains(e.AuditEventId)).ToList();

        db.IgnoredAuditEvents.RemoveRange(ignoreEventRecords);

        await db.SaveChangesAsync();

        Console.WriteLine($"UNEXPECTED: Hit duplicate \"new\" events to ignore. Deleting {ignoreEventRecords.Count} old duplicates from ignore-list.");
    }
}
