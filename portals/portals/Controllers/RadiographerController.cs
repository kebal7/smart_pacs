using FellowOakDicom;
using FellowOakDicom.IO.Buffer;
using Microsoft.AspNetCore.Mvc;
using portals.services;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace portals.Controllers
{
    //[Authorize(Roles = "Radiographer")]
    public class RadiographerController : Controller
    {
        private readonly IDicomService _dicomService;
        public RadiographerController(IDicomService dicomService)
        {
            _dicomService = dicomService;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadDicom([FromForm] PatientUploadModel model)
        {
            if (model.ImageFile == null || model.ImageFile.Length == 0)
                return BadRequest("No image uploaded");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await model.ImageFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            // Convert to DICOM
            var ds = await _dicomService.CreateDicomAsync(imageBytes, new PatientData
            {
                PatientID = model.PatientID,
                PatientName = model.PatientName,
                BirthDate = model.PatientDOB.Replace("-", ""), // YYYYMMDD
                Sex = model.PatientSex,
                StudyType = model.StudyType
            });

            // For now, just print key fields
            System.Console.WriteLine("=== DICOM Generated ===");
            System.Console.WriteLine($"Patient: {ds.GetSingleValue<string>(DicomTag.PatientName)}");
            System.Console.WriteLine($"ID: {ds.GetSingleValue<string>(DicomTag.PatientID)}");
            System.Console.WriteLine($"Modality: {ds.GetSingleValue<string>(DicomTag.Modality)}");
            System.Console.WriteLine("======================");

            // Optionally, save file temporarily
            var tempPath = Path.Combine(Path.GetTempPath(), $"{model.PatientID}_{Path.GetFileName(model.ImageFile.FileName)}.dcm");
            var dicomFile = new DicomFile(ds);
            await dicomFile.SaveAsync(tempPath);

            return Ok(new { message = "DICOM created", filePath = tempPath });
        }
        
        [HttpGet]
        public IActionResult DownloadDicom(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return NotFound("DICOM file not found");

            var fileName = Path.GetFileName(filePath);
            var mimeType = "application/dicom";

            return PhysicalFile(filePath, mimeType, fileName);
        }
    }

    public class PatientUploadModel
    {
        public string PatientID { get; set; }
        public string PatientName { get; set; }
        public string PatientDOB { get; set; }
        public string PatientSex { get; set; }
        public string StudyType { get; set; }
        public IFormFile ImageFile { get; set; }
    }
}
