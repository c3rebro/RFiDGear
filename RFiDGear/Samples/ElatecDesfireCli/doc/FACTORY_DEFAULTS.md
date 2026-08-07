# Factory DESFire Card Defaults

## ⚠️ CRITICAL: DESFire Key Lengths

**DESFire key types have DIFFERENT lengths:**

- **DES** in DESFire = 2K3DES = **16 bytes** (NOT 8 bytes!)
- **3K3DES** = **24 bytes** (3 × 8-byte keys)
- **AES** = **16 bytes**

**Factory DESFire cards ship with DES (2K3DES) keys, NOT AES!**

This was causing "Access Denied" errors. The fix separates PICC authentication from application creation.

## Factory Configuration

| Component | Key Type | Key Length | Default Value |
|-----------|----------|------------|---------------|
| **PICC Master Key** | DES (2K3DES) | 16 bytes | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| All Applications | DES (2K3DES) | 16 bytes | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |

## What Changed

### Before (Broken)
```csharp
// Tried to authenticate with AES - FAILS!
await Authenticate(
    "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", // 16 bytes
    keyType: AES  // Wrong algorithm!
);
```

### After (Fixed)
```csharp
// Authenticate with 3K3DES - WORKS!
await Authenticate(
    "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", // 16 bytes
    keyType: 3K3DES  // Correct algorithm for factory!
);

// THEN create AES application
await CreateApplication(
    appKeyType: AES  // New app uses AES (16-byte keys)
);
```

## Key Type Separation

The sample now tracks **two different key types**:

### 1. PICC Key Type (for authentication)
- **Purpose**: Authenticate to card-level (PICC)
- **Factory Default**: DES/2K3DES (16 bytes)
- **Default Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- **Used when**: Creating apps, formatting card
- **Must match**: Current PICC configuration

### 2. Application Key Type (for new apps)
- **Purpose**: Key type for applications you create
- **Default**: AES (16 bytes, recommended)
- **Default Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- **Used when**: Accessing files, changing app keys
- **Can be**: DES, 3K3DES, or AES (your choice!)

## Workflow with Factory Card

```
1. Connect to Factory DESFire Card
   └─ PICC Master Key: DES (2K3DES), all zeros (16 bytes)

2. Authenticate to PICC
   ├─ Key Type: DES (2K3DES) ← Must match factory!
   ├─ Key Value: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
   └─ Auth Mode: EV1 (mode 1)

3. Create Application 0x000001
   ├─ App Key Type: AES ← Can be different!
   ├─ Max Keys: 4
   ├─ Mode: 0xE0
   └─ Settings: 0xEB

4. Application Created Successfully
   ├─ App 0x000001 now exists
   ├─ Key 0 (master): AES, all zeros (16 bytes)
   ├─ Key 1: AES, all zeros (16 bytes)
   ├─ Key 2: AES, all zeros (16 bytes)
   └─ Key 3: AES, all zeros (16 bytes)

5. Access Application Files
   ├─ Authenticate with: App Key 0 (AES, 16 bytes)
   └─ Can now create/read/write files
```

## Common Mistakes

### ❌ Wrong: Same key type for everything
```csharp
// Assumes factory card uses AES - FAILS!
var config = new Config {
    PiccKeyType = AES,  // ← WRONG for factory!
    AppKeyType = AES
};
```

### ✅ Correct: Different key types
```csharp
// Factory uses DES (2K3DES), new app uses AES - WORKS!
var config = new Config {
    PiccKeyType = DES,   // ← Correct for factory! (16 bytes)
    AppKeyType = AES     // ← Your choice for new app (16 bytes)
};
```

## When PICC Key Type Changes

If you've changed the PICC master key to AES:

```csharp
// After you changed PICC key to AES
var config = new Config {
    PiccKeyType = AES,        // ← Now use AES for PICC
    PiccKey = "01 02 03...",  // 16 bytes
    AppKeyType = AES          // New apps also AES
};
```

## Sample Default Behavior

The CLI sample defaults to:
- **PICC Key Type**: DES (0) ← Works with factory cards
- **PICC Key Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes)
- **App Key Type**: AES (2) ← Recommended for new apps
- **App Key Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes)

This allows you to authenticate to a factory card with DES (2K3DES), then create modern AES applications!

## Verification

After the fix, you should see:

```
Authenticating to PICC with DF_KEY_DES key...
Creating application with DF_KEY_AES keys...
Application 0x000001 created successfully!
  Max Keys: 4
  App Key Type: DF_KEY_AES
  Key Settings: 0xEB
```

## Key Lengths Reference

| Key Type | Bytes | Algorithm | Hex Example |
|----------|-------|-----------|-------------|
| DES (2K3DES) | **16** | Two-key Triple DES | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| 3K3DES | **24** | Three-key Triple DES | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| AES | **16** | AES-128 | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |

**CRITICAL**: Key lengths vary! DES/AES = 16 bytes, 3K3DES = 24 bytes.

**Note**: In DESFire, "DES" actually means 2K3DES (16 bytes), not single DES (8 bytes).
