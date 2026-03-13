//import { apiFetch } from './api.js';
//import { requireAuth } from './auth.js';

//requireAuth(); // ensure user is logged in

// portal.js

// -------------------------------
//  Dummy Images List
// -------------------------------
const dummyImages = [
    '../resources/dummy_image/test1.png',
    '../resources/dummy_image/test2.png',
    '../resources/dummy_image/test3.png',
    '../resources/dummy_image/test4.png'
];

let currentDicom = null;
let currentImageFile = null;
let currentImagePath = null;


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

    currentImageFile = file;

    const reader = new FileReader();
    reader.onload = (e) => updatePreview(e.target.result);
    reader.readAsDataURL(file);

    alert(`Image "${file.name}" loaded!`);
});


// -------------------------------
//  Simulate X-ray Acquisition
// -------------------------------
document.getElementById('acquireBtn').addEventListener('click', async () => {
    const randomIndex = Math.floor(Math.random() * dummyImages.length);
    const path = dummyImages[randomIndex];

    const response = await fetch(path);
    const blob = await response.blob();

    currentImageFile = new File([blob], path.split("/").pop(), { type: blob.type });

    updatePreview(URL.createObjectURL(blob));
    alert("Simulated X-Ray acquired!");
});


// -------------------------------
//  Generate Simulated DICOM
// -------------------------------
document.getElementById('uploadDicomBtn').addEventListener('click', async () => {

    if (!currentImageFile) {
        alert("Please upload an image first!");
        return;
    }

    const formData = new FormData();

    formData.append("PatientID", document.getElementById('patientId').value);
    formData.append("PatientName", document.getElementById('patientName').value);
    formData.append("PatientDOB", document.getElementById('patientDOB').value);
    formData.append("PatientSex", document.getElementById('patientSex').value);
    formData.append("StudyType", document.getElementById('studyType').value);
    formData.append("ImageFile", currentImageFile);

    try {

        const response = await fetch("http://localhost:5266/Radiographer/UploadDicom", {
            method: "POST",
            body: formData
        });

        if (!response.ok) {
            const text = await response.text();
            alert("Upload failed: " + text);
            return;
        }

        const result = await response.json();

        currentDicom = result.filePath;

        alert("DICOM created successfully!");

        console.log("Server response:", result);

    } catch (err) {
        console.error(err);
        alert("Error uploading DICOM");
    }

});


// -------------------------------
//  Download Simulated DICOM
// -------------------------------
document.getElementById('downloadDicomBtn').addEventListener('click', () => {
    if (!currentDicom) {
        alert("No DICOM available for download!");
        return;
    }

    const blob = new Blob([currentDicom], { type: 'application/json' });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = 'simulated_dicom.json';
    a.click();

    URL.revokeObjectURL(url);
});


// -------------------------------
//  Clear Everything
// -------------------------------
document.getElementById('clearFieldsBtn').addEventListener('click', () => {
    document.getElementById('patientForm').reset();
    document.getElementById('fileInput').value = "";
    currentDicom = null;
    currentImageFile = null;
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
