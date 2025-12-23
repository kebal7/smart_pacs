import { apiFetch } from './api.js';
import { requireAuth } from './auth.js';

requireAuth(); // ensure user is logged in

// Dummy images
const dummyImages = [
    '/resources/dummy_image/test1.png',
    '/resources/dummy_image/test2.png',
    '/resources/dummy_image/test3.png',
    '/resources/dummy_image/test4.png'
];

let currentDicomPath = null;
let currentImageName = null;
let currentImagePath = null;

// ----------------- Image Preview -----------------
function updatePreview(path) {
    const img = document.getElementById('preview');
    if (!img) return console.error("Preview element missing!");
    if (!path) {
        img.style.display = 'none';
        img.src = "";
        return;
    }
    img.src = path;
    img.style.display = 'block';
}

// ----------------- Fill Dummy Patient -----------------
document.getElementById('fillDummyBtn').addEventListener('click', () => {
    document.getElementById('patientId').value = `PAT-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('accessionNo').value = `ACC-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('patientName').value = 'John Doe';
    document.getElementById('patientDOB').value = '1990-01-01';
    document.getElementById('patientSex').value = 'M';
    document.getElementById('studyType').value = 'CR';
    alert("Dummy patient info filled!");
});

// ----------------- Image Upload & Preview -----------------
const fileInput = document.getElementById("fileInput");
fileInput.addEventListener("change", (event) => {
    const file = event.target.files[0];
    if (!file) return;
    currentImageName = file.name;
    const reader = new FileReader();
    reader.onload = (e) => updatePreview(e.target.result);
    reader.readAsDataURL(file);
    alert(`Image "${file.name}" loaded!`);
});

// ----------------- Simulate X-Ray -----------------
document.getElementById('acquireBtn').addEventListener('click', () => {
    const randomIndex = Math.floor(Math.random() * dummyImages.length);
    currentImagePath = dummyImages[randomIndex];
    currentImageName = currentImagePath;
    updatePreview(currentImagePath);
    alert("Simulated X-Ray acquired!");
});

// ----------------- Upload DICOM -----------------
document.getElementById('uploadDicomBtn').addEventListener('click', async () => {
    const file = fileInput.files[0];
    if (!file) return alert("Please upload an image first!");

    const formData = new FormData();
    formData.append("ImageFile", file);
    formData.append("PatientID", document.getElementById('patientId').value);
    formData.append("PatientName", document.getElementById('patientName').value);
    formData.append("PatientDOB", document.getElementById('patientDOB').value);
    formData.append("PatientSex", document.getElementById('patientSex').value);
    formData.append("StudyType", document.getElementById('studyType').value);

    try {
        const result = await apiFetch("http://localhost:5266/Radiographer/UploadDicom", {
            method: "POST",
            body: formData
        });
        currentDicomPath = result.filePath;
        alert("DICOM uploaded and printed to server console!");
    } catch (err) {
        alert("Upload failed: " + err.message);
    }
});

// ----------------- Download DICOM -----------------
document.getElementById('downloadDicomBtn').addEventListener('click', () => {
    if (!currentDicomPath) return alert("No DICOM available for download!");
    window.location.href = `http://localhost:5266/Radiographer/DownloadDicom?filePath=${encodeURIComponent(currentDicomPath)}`;
});

// ----------------- Clear Form -----------------
document.getElementById('clearFieldsBtn').addEventListener('click', () => {
    document.getElementById('patientForm').reset();
    fileInput.value = "";
    currentDicomPath = null;
    currentImageName = null;
    currentImagePath = null;
    updatePreview(null);
    alert("Form and image preview cleared!");
});
