// radiographer-portal.js

// -------------------------------
//  Dummy Images List
// -------------------------------
const dummyImages = [
    '/resources/dummy_image/test1.png',
    '/resources/dummy_image/test2.png',
    '/resources/dummy_image/test3.png',
    '/resources/dummy_image/test4.png'
];

let currentDicom = null;
let currentImageName = null;
let currentImagePath = null;
let currentDicomPath = null;

// -------------------------------
//  Fill Dummy Patient Data
// -------------------------------
function fillDummyData() {
    document.getElementById('patientId').value = `PAT-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('accessionNo').value = `ACC-${Math.floor(Math.random() * 90000 + 10000)}`;
    document.getElementById('patientName').value = 'John Doe';
    document.getElementById('patientDOB').value = '1990-01-01';
    document.getElementById('patientSex').value = 'M';
    document.getElementById('studyType').value = 'CR';
}


// -------------------------------
//  Image Preview Helper
// -------------------------------
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


// -------------------------------
//  Handle Manual Image Upload
// -------------------------------
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


// -------------------------------
//  Simulate X-ray Acquisition
// -------------------------------
document.getElementById('acquireBtn').addEventListener('click', () => {
    const randomIndex = Math.floor(Math.random() * dummyImages.length);
    currentImagePath = dummyImages[randomIndex];
    currentImageName = currentImagePath;

    updatePreview(currentImagePath);
    alert("Simulated X-Ray acquired!");
});


// -------------------------------
//  Generate Simulated DICOM
// -------------------------------
document.getElementById('uploadDicomBtn').addEventListener('click', async () => {
    const fileInput = document.getElementById('fileInput');
    if (!fileInput.files[0]) {
        alert("Please upload an image first!");
        return;
    }

    const formData = new FormData();
    formData.append("ImageFile", fileInput.files[0]);
    formData.append("PatientID", document.getElementById('patientId').value);
    formData.append("PatientName", document.getElementById('patientName').value);
    formData.append("PatientDOB", document.getElementById('patientDOB').value);
    formData.append("PatientSex", document.getElementById('patientSex').value);
    formData.append("StudyType", document.getElementById('studyType').value);

    const response = await fetch('/Radiographer/UploadDicom', {
        method: 'POST',
        body: formData
    });

    const result = await response.json();
    currentDicomPath = result.filePath;
    console.log("Server response:", result);
    alert("DICOM uploaded and printed to server console!");
});


// -------------------------------
//  Download Simulated DICOM
// -------------------------------
document.getElementById('downloadDicomBtn').addEventListener('click', async () => {
    if (!currentDicomPath) {
        alert("No DICOM available for download!");
        return;
    }

    // Open download link
    window.location.href = `/Radiographer/DownloadDicom?filePath=${encodeURIComponent(currentDicomPath)}`;
});


// -------------------------------
//  Clear Everything
// -------------------------------
document.getElementById('clearFieldsBtn').addEventListener('click', () => {
    document.getElementById('patientForm').reset();
    document.getElementById('fileInput').value = "";
    currentDicom = null;
    currentImageName = null;
    currentImagePath = null;

    updatePreview(null);

    alert("Form and image preview cleared!");
});


// -------------------------------
//  Fill Dummy Button
// -------------------------------
document.getElementById('fillDummyBtn').addEventListener('click', () => {
    fillDummyData();
    alert("Dummy patient info filled!");
});