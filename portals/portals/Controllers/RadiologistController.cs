using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using portals.Data;

namespace portals.Controllers;

    [ApiController]
    [Route("api/[controller]")]
    public class RadiologistController : Controller
    {
        private readonly string orthancUrl = "http://localhost:8042";
        private readonly string orthancUser = "orthanc";
        private readonly string orthancPassword = "orthanc";

        private readonly ApplicationDbContext _context;
        
        public RadiologistController(ApplicationDbContext context)
        {
            _context = context;
        }
        
        private HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(orthancUrl);
            var byteArray = Encoding.ASCII.GetBytes($"{orthancUser}:{orthancPassword}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return client;
        }

        // GET: /Radiologist
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Radiologist/ListDicoms
        [HttpGet]
        public async Task<IActionResult> ListDicoms()
        {
            using var client = CreateClient();
            var response = await client.GetAsync("instances");
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM instances from Orthanc");

            var instanceIds = await response.Content.ReadAsStringAsync();
            return Content(instanceIds, "application/json");
        }
        
        

        // GET: /Radiologist/GetDicomMetadata?instanceId=xxx
        [HttpGet]
        public async Task<IActionResult> GetDicomMetadata(string instanceId)
        {
            using var client = CreateClient();
            var response = await client.GetAsync($"instances/{instanceId}/tags");
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM metadata");

            var metadata = await response.Content.ReadAsStringAsync();
            return Content(metadata, "application/json");
        }

        // GET: /Radiologist/DownloadDicom?instanceId=xxx
        [HttpGet("DownloadDicom")]
        public async Task<IActionResult> DownloadDicom(string instanceId)
        {
            using var client = CreateClient();

            // 1. Get metadata to build a proper filename
            var metaRes = await client.GetAsync($"instances/{instanceId}/tags");
            if (!metaRes.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM metadata");

            var meta = await metaRes.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

            string patientName = meta?["0010,0010"].GetProperty("Value").GetString() ?? "UnknownPatient";
            string patientId = meta?["0010,0020"].GetProperty("Value").GetString() ?? instanceId;
            string studyDate = meta?["0008,0020"].GetProperty("Value").GetString() ?? DateTime.Now.ToString("yyyyMMdd");

            // Clean filename
            string safePatientName = patientName.Replace(" ", "_");
            string fileName = $"{patientId}-{safePatientName}-{studyDate}.dcm";

            // 2. Get the actual DICOM bytes
            var dicomRes = await client.GetAsync($"instances/{instanceId}/file");
            if (!dicomRes.IsSuccessStatusCode)
                return StatusCode(500, "Failed to fetch DICOM file");

            var bytes = await dicomRes.Content.ReadAsByteArrayAsync();

            // 3. Return file to browser
            return File(bytes, "application/dicom", fileName);
        }
        
        [HttpGet("GetWorklist")]
        public async Task<IActionResult> GetWorklist()
        {
            // Fetch directly from Reports
            var worklist = await _context.Reports
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.InstanceNumber,
                    r.ReportIdentifier,
                    r.PatientIdentifier,
                    r.PatientName,
                    r.Modality,
                    r.CreatedAt,
                    r.AiSuggestion,
                    r.IsFinalized,
                    r.Findings,
                    // Calculate a status string for the UI
                    Status = r.IsFinalized ? "Finalized" : 
                        (!string.IsNullOrEmpty(r.Findings) ? "Draft" : "New")
                })
                .ToListAsync();

            return Ok(worklist);
        }
        
        // Inside RadiologistController.cs

        [HttpPost("UpdateReport")]
        public async Task<IActionResult> UpdateReport([FromBody] UpdateReportRequest model)
        {
            var report = await _context.Reports.FirstOrDefaultAsync(r => r.InstanceNumber == model.InstanceId);
            if (report == null) return NotFound("Report not found.");
            if (report.IsFinalized) return BadRequest("Report is finalized Cannot Update Report.");
            report.StudyDescription = model.StudyDescription;
            report.ClinicalHistory = model.ClinicalHistory;
            report.Findings = model.Findings;
            report.Impression = model.Impression;
            report.OtherNote = model.OtherNote;
    
            if (!string.IsNullOrEmpty(model.AiSuggestion)) 
            {
                report.AiSuggestion = model.AiSuggestion;
            }

            // 2. Logic: Define "Draft" vs "Finalized"
            if (model.ShouldFinalize)
            {
                report.IsFinalized = true;
                report.FinalizedAt = DateTime.UtcNow;
                report.FinalizedBy = "Dr. Senior User"; // Replace with real Auth later
            }
            else 
            {
                // This is a "Save Draft" action
                report.IsFinalized = false; 
                report.GeneratedBy = "Dr. Junior User";
                report.GeneratedAt = DateTime.UtcNow; // Mark that work has started
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = report.IsFinalized ? "Finalized!" : "Draft Saved!" });
        }

        public class UpdateReportRequest
        {
            public string InstanceId { get; set; } // Matches JS InstanceId
            public string StudyDescription { get; set; }
            public string ClinicalHistory { get; set; }
            public string Findings { get; set; }
            public string Impression { get; set; }
            public string OtherNote { get; set; }
            public string AiSuggestion { get; set; }
            public bool ShouldFinalize { get; set; } // Matches JS ShouldFinalize
        }
        
        // Add this to RadiologistController.cs

        [HttpGet("GetReport")]
        public async Task<IActionResult> GetReport(string instanceId)
        {
            // Search by the Orthanc Instance UID
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.InstanceNumber == instanceId);

            if (report == null) return NotFound();

            // Return EVERYTHING so the UI can repopulate
            return Ok(new
            {
                studyDescription = report.StudyDescription,
                clinicalHistory = report.ClinicalHistory,
                findings = report.Findings,
                impression = report.Impression,
                otherNote = report.OtherNote,
                aiSuggestion = report.AiSuggestion,
                isFinalized = report.IsFinalized
            });
        }
        
        [HttpGet("GetPrintingReport")]
        public async Task<IActionResult> GetPrintingReport(string instanceId)
        {
            var report = await _context.Reports
                .FirstOrDefaultAsync(r => r.InstanceNumber == instanceId);

            if (report == null) return NotFound("Report record missing.");

            // Return every single field for the print template
            return Ok(report); 
        }
    }
