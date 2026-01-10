import pandas as pd

# ================================
# Settings
# ================================
input_csv = "Data_Entry_2017_v2020.csv"
output_csv = "chestxray14_PA_encoded.csv"

all_labels = [
    'Atelectasis','Cardiomegaly','Effusion','Infiltration','Mass',
    'Nodule','Pneumonia','Pneumothorax','Consolidation','Edema',
    'Emphysema','Fibrosis','Pleural_Thickening','Hernia'
]

# ================================
# Load original metadata
# ================================
df = pd.read_csv(input_csv)

# ================================
# Filter PA views only
# ================================
df = df[df['View Position'] == 'PA'].copy()

# ================================
# Encode multi-labels
# ================================
for label in all_labels:
    df[label] = df['Finding Labels'].apply(
        lambda x: 1 if label in x else 0
    )

# ================================
# Handle "No Finding"
# (all disease labels = 0)
# ================================
no_finding_mask = df['Finding Labels'] == 'No Finding'
df.loc[no_finding_mask, all_labels] = 0

# ================================
# Save new CSV
# ================================
df.to_csv(output_csv, index=False)

print("✅ Encoded CSV saved as:", output_csv)
print("Total PA images:", len(df))
print("Example rows:")
print(df.head())
