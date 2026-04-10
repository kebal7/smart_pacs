using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portals.Models;

[Table("patient_timeline")] // Keeps your DB naming consistent
public class TimelineEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("patient_id")]
    public int PatientId { get; set; }

    [Column("case_id")]
    public int? CaseId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; }

    [Required]
    [Column("event_date")]
    public DateTime EventDate { get; set; }

    [Column("source_table")]
    public string SourceTable { get; set; } 

    [Column("source_id")]
    public int? SourceId { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("PatientId")]
    public virtual Patient Patient { get; set; }
}