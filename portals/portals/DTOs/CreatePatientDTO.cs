using System;

namespace portals.DTOs;

public class CreatePatientDto
{
    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Address { get; set; }
    public string ContactNo { get; set; }
    public string EmergencyContact { get; set; }
}