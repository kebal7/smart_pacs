using FellowOakDicom;
using FellowOakDicom.IO.Buffer;
using Microsoft.AspNetCore.Mvc;
using portals.services;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using portals.Data;

namespace portals.Controllers
{
    //[Authorize(Roles = "Radiographer")]
    public class RadiographerController : Controller
    {
        private readonly IDicomService _dicomService;
        private readonly ApplicationDbContext _context;
        
        public RadiographerController(IDicomService dicomService, ApplicationDbContext context)
        {
            _dicomService = dicomService;
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadDicom([FromForm] PatientUploadModel model)
        {
            // 1. Read uploaded image
            if (model.ImageFile == null || model.ImageFile.Length == 0)
                return BadRequest("No image uploaded");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await model.ImageFile.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            //2. Convert to DICOM
            var ds = await _dicomService.CreateDicomAsync(imageBytes, new PatientData
            {
                PatientID = model.PatientID,
                PatientName = model.PatientName,
                BirthDate = model.PatientDOB.Replace("-", ""), // YYYYMMDD
                Sex = model.PatientSex,
                StudyType = model.StudyType
            });
            
            Console.WriteLine(ds);
            // 3. Save to MemoryStream (no temp file)
            var msDicom = new MemoryStream();
            var dicomFile = new DicomFile(ds);
            await dicomFile.SaveAsync(msDicom);
            msDicom.Position = 0;
            
            Console.WriteLine("UploadDicom called");
            Console.WriteLine($"File length: {model.ImageFile?.Length}");
            
            // 4. Upload to Orthanc
            using var client = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes("orthanc:orthanc");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            var content = new MultipartFormDataContent();
            content.Add(new StreamContent(msDicom), "file", $"{model.PatientID}.dcm");

            var response = await client.PostAsync("http://localhost:8042/instances", content);
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Failed to upload to Orthanc");

            var orthancResult = await response.Content.ReadAsStringAsync();
            Console.WriteLine(orthancResult);
            
            return Ok(new
            {
                message = "DICOM created and uploaded to Orthanc",
                orthancResponse = orthancResult
            });
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
        
        [HttpGet("api/patients/{patientId}")]
        public IActionResult GetPatientByIdentifier(string patientId)
        {
            var patient = _context.Patients.FirstOrDefault(p => p.PatientIdentifier == patientId);
            if (patient == null) return NotFound();
            return Ok(patient);
        }

        // Optional: generate new Accession & Study ID
        [HttpGet("api/study/new")]
        public IActionResult GenerateNewStudy()
        {
            var nextAccession = $"ACC-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var nextStudy = $"STUDY-{DateTime.UtcNow:yyyyMMddHHmmss}";
            return Ok(new { accessionNo = nextAccession, studyId = nextStudy });
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
