using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities;

[Table("copilot_activities")]
public class CopilotActivity : AbstractEFEntityWithName
{
    [ForeignKey(nameof(ActivityType))]
    [Column("activity_type_id")]
    public int ActivityTypeId { get; set; }

    public CopilotActivityType ActivityType { get; set; } = null!;
}

[Table("copilot_activity_types")]
public class CopilotActivityType : AbstractEFEntityWithName
{
    public const string Document = "Document";
    public const string Meeting = "Meeting";
    public const string Email = "Email";
    public const string Chat = "Chat";
    public const string Other = "Other";
}

[Table("copilot_agents")]
public class CopilotAgent : AbstractEFEntityWithName
{
    [Column("agent_id")]
    public string AgentID { get; set; } = null!;
}
