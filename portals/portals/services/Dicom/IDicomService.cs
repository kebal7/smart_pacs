using FellowOakDicom;

namespace portals.services;

public interface IDicomService
{
    Task<DicomDataset> CreateDicomAsync(byte[] imageBytes, PatientData patient);
}