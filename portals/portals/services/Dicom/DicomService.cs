using FellowOakDicom;
using FellowOakDicom.IO.Buffer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading.Tasks;

namespace portals.services;

public class PatientData
{
    public string PatientID { get; set; }
    public string PatientName { get; set; }
    public string BirthDate { get; set; } // YYYYMMDD
    public string Sex { get; set; }       // "M", "F", "O"
    public string StudyType { get; set; } = "CR";
    public string BodyPart { get; set; } = "CHEST";
    public string ViewPosition { get; set; } = "PA";
    
    public string AccessionNumber { get; set; } 
    public string StudyID { get; set; }
}

public class DicomService : IDicomService
{
    public async Task<DicomDataset> CreateDicomAsync(byte[] imageBytes, PatientData patient)
    {
        using var img = Image.Load<Rgba32>(imageBytes);

        var pixels = new byte[img.Width * img.Height];

        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                var p = img[x, y];
                // Convert to grayscale using luminance formula
                pixels[y * img.Width + x] = (byte)(0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
            }
        }

        var ds = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PatientName, patient.PatientName },
            { DicomTag.PatientID, patient.PatientID },
            { DicomTag.PatientBirthDate, patient.BirthDate },
            { DicomTag.PatientSex, patient.Sex },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.Modality, patient.StudyType },
            { DicomTag.BodyPartExamined, patient.BodyPart },
            { DicomTag.ViewPosition, patient.ViewPosition },
            { DicomTag.StudyDate, DateTime.Now.ToString("yyyyMMdd") },
            { DicomTag.StudyTime, DateTime.Now.ToString("HHmmss") },
            { DicomTag.Rows, (ushort)img.Height },
            { DicomTag.Columns, (ushort)img.Width },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.StudyID, patient.StudyID },
            { DicomTag.AccessionNumber, patient.AccessionNumber },
        };

        ds.AddOrUpdate(new DicomOtherByte(DicomTag.PixelData, pixels));

        return ds;
    }
}
