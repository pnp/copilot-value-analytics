using Entities.DB.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities.Profiling;


/// <summary>
/// Activities in a service in O365 ('Teams Post Messages'), per activity + user, per week
/// </summary>
public class ActivitiesWeekly : IActivitiesWeeklyRecord
{
    [Key]
    public string Metric { get; set; } = string.Empty;
    public int Sum { get; set; } = 0;

    [Key]
    public DateOnly MetricDate { get; set; }

    public User User { get; set; } = null!;

    [ForeignKey(nameof(User))]
    [Column("user_id")]
    [Key]
    public int UserID { get; set; }

    ITrackedUser IActivitiesWeeklyRecord.User { get => User; }
}
