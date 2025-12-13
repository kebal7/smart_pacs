from generate_dicom import create_dicom_from_image

import requests
from io import BytesIO

AUTH = ("orthanc", "orthanc")

# Example usage
dicom_obj = create_dicom_from_image(
    "../resources/dummy_images/chest-x-ray-image.jpg",
    "Prashant Rijal",
    "PRC986",
    "19320805",
    "M",
    "output-dicoms"
)

# dicom_obj is pydicom Dataset
buf = BytesIO()
dicom_obj.save_as(buf, write_like_original=False)
buf.seek(0)  # important: reset pointer to start


#r = requests.post(
#    "http://localhost:8042/instances",
#    data=buf.read(),
#    auth=AUTH,
#    headers={"Content-Type": "application/dicom"}
#)

print("===============================================================================================")
print(dicom_obj)
print("===============================================================================================")
print("===============================================================================================")
#print(r.status_code)
#print("===============================================================================================")
#print(r.json())

