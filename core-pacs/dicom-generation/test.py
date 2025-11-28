from pydicom import dcmread
from pydicom.data import get_testdata_file

filename = get_testdata_file("CT_small.dcm")
ds = dcmread(filename)
print(type(ds))

# elem = ds[0x0008, 0x0016]
# print(elem)

elem = ds['SOPClassUID']
print(elem)
#print(ds)

# print(ds.PatientName)
# print(ds.PatientID)
# print(ds.Modality)
# print(ds.StudyInstanceUID)