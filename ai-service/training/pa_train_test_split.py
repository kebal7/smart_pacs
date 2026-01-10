import pandas as pd

# ================================
# Settings
# ================================
encoded_csv = "chestxray14_PA_encoded.csv"
train_val_list_file = "train_val_list.txt"
test_list_file = "test_list.txt"

train_out = "chestxray14_PA_train.csv"
test_out = "chestxray14_PA_test.csv"

# ================================
# Load encoded dataset
# ================================
df = pd.read_csv(encoded_csv)

# ================================
# Load split lists
# ================================
with open(train_val_list_file, "r") as f:
    train_files = set(line.strip() for line in f)

with open(test_list_file, "r") as f:
    test_files = set(line.strip() for line in f)

# ================================
# Apply splits
# ================================
train_df = df[df["Image Index"].isin(train_files)].copy()
test_df  = df[df["Image Index"].isin(test_files)].copy()

# ================================
# Sanity checks
# ================================
assert len(set(train_df["Image Index"]) & set(test_df["Image Index"])) == 0, \
    "❌ Data leakage detected!"

print("✅ Train images:", len(train_df))
print("✅ Test images :", len(test_df))

# ================================
# Save CSVs
# ================================
train_df.to_csv(train_out, index=False)
test_df.to_csv(test_out, index=False)

print("📁 Saved:", train_out)
print("📁 Saved:", test_out)
