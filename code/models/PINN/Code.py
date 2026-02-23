# =============================================================================
# 1. INSTALL & IMPORT
# =============================================================================
!pip install -q wandb

import tensorflow as tf
from tensorflow.keras import layers, models
import pandas as pd
import numpy as np
import pickle
import os
import matplotlib.pyplot as plt
from scipy.interpolate import interp1d
from google.colab import drive
import wandb
from sklearn.metrics import r2_score
from sklearn.model_selection import train_test_split

# =============================================================================
# 2. WANDB INIT
# =============================================================================
wandb.login()

wandb.init(
    project="PINN-CDP",
    name="digital-twin-investigator-run-v2",
    config={
        "epochs": 100,
        "batch_size": 512,
        "learning_rate": 0.001,
        "physics_weight": 0.01,
        "architecture": "64-128-128-64 tanh",
        "input_features": ["x", "y", "load_mag", "global_deflection", "fc", "fy"],
        "output_targets": ["strain", "stress", "damage"]
    }
)

# =============================================================================
# 3. SETUP: CONNECT TO DRIVE & LOAD DATA
# =============================================================================
print("--- Mounting Google Drive ---")
drive.mount('/content/drive')

BASE_PATH = "/content/drive/My Drive/PINN_Project/"
DATA_PATH = os.path.join(BASE_PATH, "pinn_master_training_data.csv")
SCALER_PATH = os.path.join(BASE_PATH, "pinn_scaler_params.pkl")
EXCEL_FILE = os.path.join(BASE_PATH, "CDP properties for validation.xlsx")

if not os.path.exists(DATA_PATH):
    raise FileNotFoundError(f"Files not found at {BASE_PATH}. Check your Drive folder.")

print("Loading data...")
df = pd.read_csv(DATA_PATH)
with open(SCALER_PATH, 'rb') as f:
    scaler = pickle.load(f)

# Load CDP curves from Excel
df_comp_raw = pd.read_excel(EXCEL_FILE, sheet_name='compression (grade 25 concrete)', header=None)
df_tens_raw = pd.read_excel(EXCEL_FILE, sheet_name='tension (grade 25 concrete)', header=None)

# =============================================================================
# 4. CDP PHYSICS CURVE PROCESSING
# =============================================================================
def get_columns_by_header(df_raw):
    df_str = df_raw.astype(str).apply(lambda x: x.str.lower())
    stress_col, strain_col, start_row = None, None, None
    for r in range(min(20, len(df_raw))):
        row_vals = df_str.iloc[r].values
        for c, val in enumerate(row_vals):
            if "inelastic" in val or "cracking" in val:
                strain_col = c
                start_row = r
            if "stress" in val or "sigma" in val or "σ" in val:
                stress_col = c
        if stress_col is not None and strain_col is not None: break
    return stress_col, strain_col, start_row

def extract_clean_cdp_data(df_raw, type='comp'):
    col_stress, col_strain, start_row = get_columns_by_header(df_raw)
    data = df_raw.iloc[start_row+1:, [col_stress, col_strain]].copy()
    data.columns = ["Stress", "Strain_Ine"]
    data = data.apply(pd.to_numeric, errors='coerce').dropna()
    
    stress = data["Stress"].values
    strain_ine = data["Strain_Ine"].values
    E_conc = 25000.0 # Standard Elastic Modulus
    
    if type == 'comp':
        stress = -np.abs(stress)
        strain = - (np.abs(strain_ine) + (np.abs(stress) / E_conc))
    else:
        stress = np.abs(stress)
        strain = np.abs(strain_ine) + (np.abs(stress) / E_conc)
    return strain, stress

print("Processing Physics Curves...")
comp_strain, comp_stress = extract_clean_cdp_data(df_comp_raw, 'comp')
tens_strain, tens_stress = extract_clean_cdp_data(df_tens_raw, 'tens')

FC_REF_FILE = np.max(np.abs(comp_stress))
full_strain = np.concatenate([comp_strain[::-1], [0], tens_strain])
full_stress = np.concatenate([comp_stress[::-1], [0], tens_stress])
stress_strain_func = interp1d(full_strain, full_stress, kind='linear', fill_value="extrapolate")

def get_base_physics_stress(strain_tensor):
    return tf.numpy_function(
        func=lambda x: stress_strain_func(x).astype(np.float32),
        inp=[strain_tensor],
        Tout=tf.float32
    )

# =============================================================================
# 5. DATA PREPARATION (CRITICAL FIX: NORMALIZATION)
# =============================================================================
X_cols = ['x', 'y', 'load_mag', 'global_deflection', 'fc', 'fy']
Y_cols = ['strain', 'stress', 'damage']

# 1. Get Raw Data
X_raw = df[X_cols].values.astype(np.float32)
Y_raw = df[Y_cols].values.astype(np.float32)

# 2. Normalize Function
def normalize_data(data, columns, scaler_dict):
    data_norm = np.copy(data)
    for i, col in enumerate(columns):
        if col in scaler_dict:
            col_min = scaler_dict[col]['min']
            col_max = scaler_dict[col]['max']
            # Avoid division by zero
            denom = col_max - col_min
            if denom == 0: denom = 1.0
            data_norm[:, i] = (data[:, i] - col_min) / denom
    return data_norm

print("Normalizing Data...")
X_norm = normalize_data(X_raw, X_cols, scaler)
Y_norm = normalize_data(Y_raw, Y_cols, scaler)

# 3. Split (Using NORMALIZED Data)
X_train, X_val, Y_train, Y_val = train_test_split(X_norm, Y_norm, test_size=0.2, random_state=42)

# 4. Convert to Tensors
X_train_tf = tf.convert_to_tensor(X_train)
Y_train_tf = tf.convert_to_tensor(Y_train)
X_val_tf = tf.convert_to_tensor(X_val)
Y_val_tf = tf.convert_to_tensor(Y_val)

print(f"Training Samples: {len(X_train)}")
print(f"Validation Samples: {len(X_val)}")
print(f"Max scaled val check: {np.max(X_train):.2f} (Should be ~1.0)")

# =============================================================================
# 6. PINN LOSS & MODEL ARCHITECTURE
# =============================================================================
def pinn_loss(y_true, y_pred, x_input):
    # 1. Data Match Loss (MSE)
    mse_data = tf.reduce_mean(tf.square(y_true - y_pred))
    
    # 2. Physics Match Loss
    pred_strain_norm = y_pred[:, 0]
    pred_stress_norm = y_pred[:, 1]
    
    # Unscale predictions to real units for physics check
    eps_real = pred_strain_norm * (scaler['strain']['max'] - scaler['strain']['min']) + scaler['strain']['min']
    sig_real = pred_stress_norm * (scaler['stress']['max'] - scaler['stress']['min']) + scaler['stress']['min']
    
    # Unscale fc (Index 4)
    fc_norm = x_input[:, 4]
    fc_actual = tf.abs(fc_norm * (scaler['fc']['max'] - scaler['fc']['min']) + scaler['fc']['min'])
    
    # Calculate non-linear physics stress from CDP curve
    base_stress = get_base_physics_stress(eps_real)
    scaling_factor = fc_actual / FC_REF_FILE
    target_physics_stress = base_stress * scaling_factor
    
    mse_physics = tf.reduce_mean(tf.square(sig_real - target_physics_stress))
    
    return mse_data + 0.01 * mse_physics

def build_model():
    model = models.Sequential([
        layers.Input(shape=(6,)), 
        layers.Dense(64, activation='tanh'),
        layers.Dense(128, activation='tanh'),
        layers.Dense(128, activation='tanh'),
        layers.Dense(64, activation='tanh'),
        layers.Dense(3) 
    ])
    return model

model = build_model()
lr_schedule = tf.keras.optimizers.schedules.ExponentialDecay(0.001, 1000, 0.9)
optimizer = tf.keras.optimizers.Adam(learning_rate=lr_schedule, clipnorm=1.0)

# =============================================================================
# 7. TRAINING LOOP
# =============================================================================
@tf.function
def train_step(x, y):
    with tf.GradientTape() as tape:
        preds = model(x, training=True)
        loss = pinn_loss(y, preds, x)
    grads = tape.gradient(loss, model.trainable_variables)
    optimizer.apply_gradients(zip(grads, model.trainable_variables))
    return loss

train_dataset = tf.data.Dataset.from_tensor_slices((X_train_tf, Y_train_tf)).shuffle(2000).batch(512)

print("Starting Training...")
for epoch in range(100):
    epoch_loss = []
    for xb, yb in train_dataset:
        loss_val = train_step(xb, yb)
        epoch_loss.append(loss_val)
    
    avg_loss = np.mean(epoch_loss)
    
    # Validation Metrics (R2 Only)
    preds_val = model.predict(X_val_tf, verbose=0)
    val_r2 = r2_score(Y_val, preds_val)
    
    wandb.log({"epoch": epoch, "total_loss": avg_loss, "overall_r2_score": val_r2})
    
    if epoch % 10 == 0:
        print(f"Epoch {epoch} | Loss: {avg_loss:.5f} | R2: {val_r2:.4f}")

# =============================================================================
# 8. SAVE & LOG
# =============================================================================
save_path = os.path.join(BASE_PATH, "pinn_investigator_final.h5")
model.save(save_path)

artifact = wandb.Artifact("pinn_investigator_model", type="model")
artifact.add_file(save_path)
wandb.log_artifact(artifact)

# Visualize Physics Check (on 1000 validation samples)
preds_sample = model.predict(X_val_tf[:1000], verbose=0)
# Unscale for plotting
pred_strain = preds_sample[:, 0] * (scaler['strain']['max'] - scaler['strain']['min']) + scaler['strain']['min']
pred_stress = preds_sample[:, 1] * (scaler['stress']['max'] - scaler['stress']['min']) + scaler['stress']['min']

plt.figure(figsize=(8, 6))
plt.plot(full_strain, full_stress, 'k-', label="Base CDP Curve")
plt.scatter(pred_strain, pred_stress, s=10, c='red', alpha=0.3, label="PINN Predictions")
plt.xlabel("Strain"), plt.ylabel("Stress (MPa)"), plt.title("Physics Verification")
plt.legend(), plt.grid(True)
wandb.log({"physics_verification": wandb.Image(plt)})

wandb.finish()
print("✅ Digital Twin Training Complete!")