using Common.DataUtils.Sql;

namespace ActivityImporter.Engine.ActivityAPI.Models;


public abstract class BaseCopilotLogTempEntity
{
    [Column("app_host")]
    public required string AppHost { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("agent_name", true)]
    public string? AgentName { get; set; }

    [Column("agent_id", true)]
    public string? AgentId { get; set; }
}

[TempTableName(ActivityImportConstants.STAGING_TABLE_COPILOT_CHATONLY)]
public class ChatOnlyCopilotLogTempEntity : BaseCopilotLogTempEntity
{
}

[TempTableName(ActivityImportConstants.STAGING_TABLE_COPILOT_TEAMS)]
internal class TeamsCopilotLogTempEntity : BaseCopilotLogTempEntity
{

    [Column("meeting_id")]
    public required string MeetingId { get; internal set; }

    [Column("meeting_created_utc")]
    public DateTime MeetingCreatedUTC { get; internal set; }

    [Column("meeting_name")]
    public required string MeetingName { get; internal set; }
}

/// <summary>
/// SharePoint event temp entity
/// </summary>
[TempTableName(ActivityImportConstants.STAGING_TABLE_COPILOT_SP)]
internal class SPCopilotLogTempEntity : BaseCopilotLogTempEntity
{
    [Column("url_base", true)]
    public required string UrlBase { get; set; }

    [Column("file_name")]
    public required string FileName { get; set; }

    [Column("file_extension")]
    public required string FileExtension { get; set; }

    [Column("url")]
    public required string Url { get; set; }
}


/// <summary>
/// Class for inserting staging data to temp SQL table
/// </summary>
[TempTableName(ActivityImportConstants.STAGING_TABLE_ACTIVITY_SP)]
internal class SPAuditLogTempEntity
{
    public SPAuditLogTempEntity(AbstractAuditLogContent abtractLog, string userNameOrHash)
    {

        Id = abtractLog.Id;
        UserName = userNameOrHash;
        OperationName = abtractLog.Operation;
        TimeStamp = abtractLog.CreationTime;
        TypeName = abtractLog.ItemType;
        ObjectId = abtractLog.ObjectId;
        Workload = abtractLog.Workload;

        if (abtractLog is SharePointAuditLogContent)
        {
            var spLog = (SharePointAuditLogContent)abtractLog;

            FileName = spLog.SourceFileName;
            ExtensionName = spLog.SourceFileExtension;
            UrlBase = spLog.SiteUrl;
        }
    }

    [Column("log_id")]
    public Guid Id { get; set; } = Guid.Empty;

    [Column("user_name")]
    public string UserName { get; set; } = null!;

    [Column("file_name", true)]
    public string FileName { get; set; } = null!;

    [Column("extension_name", true)]
    public string ExtensionName { get; set; } = null!;

    [Column("operation_name")]
    public string OperationName { get; set; } = null!;

    [Column("time_stamp")]
    public DateTime TimeStamp { get; set; } = DateTime.MinValue;

    [Column("workload")]
    public string Workload { get; set; } = null!;

    [Column("type_name")]
    public string TypeName { get; set; } = null!;

    [Column("object_id")]
    public string ObjectId { get; set; } = null!;

    [Column("url_base", true)]
    public string? UrlBase { get; set; } = null!;
}

