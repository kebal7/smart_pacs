using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace portals.Models;

[Table("patients")]
public class Patient
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("patient_identifier")]
    public string PatientIdentifier { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("date_of_birth")]
    public DateTime DateOfBirth { get; set; }

    [Column("address")]
    public string Address { get; set; }

    [Column("contact_no")]
    public string ContactNo { get; set; }

    [Column("emergency_contact")]
    public string EmergencyContact { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}