using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace portals.Controllers
{
    public class RadiologistController : Controller
    {
        private readonly string orthancUrl = "http://localhost:8042";
        private readonly string orthancUser = "orthanc";
        private readonly string orthancPassword = "orthanc";

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
        // GET: /Radiologist/DownloadDicom?instanceId=xxx
        [HttpGet]
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
    }
}