namespace portals.DTOs;

public class CreateReportDto
{
    public string PatientIdentifier { get; set; }

    public string StudyIdentifier { get; set; }

    public string InstanceNumber { get; set; }
}