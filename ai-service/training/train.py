# ================================ 
# 1️⃣ Imports 
# ================================
import os
import pandas as pd
from PIL import Image
import torch
from torch.utils.data import Dataset, DataLoader
from torchvision import transforms, models
import torch.nn as nn
import torch.optim as optim
from torchvision.models import DenseNet121_Weights
from sklearn.metrics import roc_auc_score

# ================================ 
# 2️⃣ Settings 
# ================================
data_dir = r"/run/media/kebal/kebal-external-ssd/images/"
metadata_csv = "metadata.csv"
train_val_list_file = "train_val_list.txt"
test_list_file = "test_list.txt"

image_size = 224
batch_size = 16
num_epochs = 5
num_classes = 14
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

all_labels = ['Atelectasis','Cardiomegaly','Effusion','Infiltration','Mass',
              'Nodule','Pneumonia','Pneumothorax','Consolidation','Edema',
              'Emphysema','Fibrosis','Pleural_Thickening','Hernia']

# ================================ 
# 3️⃣ Load metadata & filter PA 
# ================================
print("[INFO] Loading metadata...")
df = pd.read_csv(metadata_csv)
df_pa = df[df['View Position'] == 'PA'].copy()

# ================================ 
# 4️⃣ Multi-label encoding 
# ================================
print("[INFO] Encoding multi-labels...")
for label in all_labels:
    df_pa[label] = df_pa['Finding Labels'].apply(lambda x: 1 if label in x else 0)

# ================================ 
# 5️⃣ Split train/test by provided txt 
# ================================
train_val_files = set(open(train_val_list_file).read().splitlines())
test_files = set(open(test_list_file).read().splitlines())

train_val_df = df_pa[df_pa['Image Index'].isin(train_val_files)]
test_df = df_pa[df_pa['Image Index'].isin(test_files)]

# ================================ 
# 6️⃣ Dataset Class 
# ================================
class ChestXrayDataset(Dataset):
    def __init__(self, df, image_dir, transform=None):
        self.df = df
        self.image_dir = image_dir
        self.transform = transform
        self.labels = df[all_labels].values
        self.filenames = df['Image Index'].values

    def __len__(self):
        return len(self.df)

    def __getitem__(self, idx):
        img_path = os.path.join(self.image_dir, self.filenames[idx])
        image = Image.open(img_path).convert("L")  # Grayscale
        if self.transform:
            image = self.transform(image)
        else:
            image = transforms.ToTensor()(image)
            image = image.repeat(3,1,1)  # convert 1-channel → 3-channel
        label = torch.tensor(self.labels[idx], dtype=torch.float32)
        return image, label

# ================================ 
# 7️⃣ DataLoaders 
# ================================
common_transform = transforms.Compose([
    transforms.Resize((image_size, image_size)),
    transforms.Grayscale(num_output_channels=3),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485,0.456,0.406], std=[0.229,0.224,0.225])
])

print("[INFO] Preparing datasets...")
train_dataset = ChestXrayDataset(train_val_df, data_dir, transform=common_transform)
test_dataset = ChestXrayDataset(test_df, data_dir, transform=common_transform)

train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True, num_workers=0)
test_loader = DataLoader(test_dataset, batch_size=batch_size, shuffle=False, num_workers=0)

print(f"[INFO] Train samples: {len(train_dataset)}")
print(f"[INFO] Test samples: {len(test_dataset)}")
print("[INFO] Dataset ready.\n")

# ================================ 
# 8️⃣ DenseNet-121 model 
# ================================
print("[INFO] Loading DenseNet-121...")
model = models.densenet121(weights=DenseNet121_Weights.DEFAULT)
model.classifier = nn.Linear(model.classifier.in_features, num_classes)
model = model.to(device)

criterion = nn.BCEWithLogitsLoss()
optimizer = optim.AdamW(model.parameters(), lr=1e-4)

# ================================ 
# 9️⃣ Resume From Checkpoint 
# ================================
resume_path = "checkpoint_latest.pth"
start_epoch = 0
if os.path.exists(resume_path):
    print(f"[INFO] Resuming from checkpoint: {resume_path}")
    checkpoint = torch.load(resume_path, map_location=device)
    model.load_state_dict(checkpoint["model_state"])
    optimizer.load_state_dict(checkpoint["optimizer_state"])
    start_epoch = checkpoint["epoch"] + 1
    print(f"[INFO] Resumed at epoch {start_epoch}\n")
else:
    print("[INFO] No checkpoint found, starting fresh.\n")

# ================================ 
# 🔟 Training Loop 
# ================================
for epoch in range(start_epoch, num_epochs):
    model.train()
    running_loss = 0.0
    print(f"\n[INFO] Starting epoch {epoch+1}/{num_epochs}")

    for batch_idx, (images, labels) in enumerate(train_loader):
        images, labels = images.to(device), labels.to(device)

        optimizer.zero_grad()
        outputs = model(images)
        loss = criterion(outputs, labels)
        loss.backward()
        optimizer.step()

        running_loss += loss.item() * images.size(0)

        if (batch_idx + 1) % 50 == 0:
            print(f" Batch {batch_idx+1}/{len(train_loader)} - Loss: {loss.item():.4f}")

    epoch_loss = running_loss / len(train_loader.dataset)
    print(f"[INFO] Epoch [{epoch+1}/{num_epochs}] Finished → Loss: {epoch_loss:.4f}")

    # 💾 SAVE CHECKPOINT
    checkpoint_data = {
        "epoch": epoch,
        "model_state": model.state_dict(),
        "optimizer_state": optimizer.state_dict()
    }
    torch.save(checkpoint_data, "checkpoint_latest.pth")
    torch.save(checkpoint_data, f"checkpoint_epoch_{epoch+1}.pth")
    print(f"[INFO] Saved checkpoint: checkpoint_epoch_{epoch+1}.pth")

# ================================ 
# 1️⃣1️⃣ Evaluation 
# ================================
model.eval()
all_preds, all_labels_list = [], []

with torch.no_grad():
    for images, labels in test_loader:
        images = images.to(device)
        outputs = torch.sigmoid(model(images))
        all_preds.append(outputs.cpu())
        all_labels_list.append(labels)

all_preds = torch.cat(all_preds)
all_labels = torch.cat(all_labels_list)

# AUROC per class
roc_scores = []
for i, label in enumerate(all_labels_list[0].columns if hasattr(all_labels_list[0], "columns") else range(num_classes)):
    roc = roc_auc_score(all_labels[:, i], all_preds[:, i])
    roc_scores.append(roc)

print("AUROC per class:", dict(zip(all_labels_list[0].columns if hasattr(all_labels_list[0], "columns") else all_labels, roc_scores)))

# ================================ 
# 1️⃣2️⃣ Save Final Model 
# ================================
torch.save(model.state_dict(), "densenet121_chestxray14.pth")
print("[INFO] Final model saved.")
