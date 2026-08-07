# DESFire Key Types Explained

## ⚠️ CRITICAL: Key Lengths Vary!

**DESFire supports three key types with different lengths:**

| Key Type | Bytes | Encryption | Factory Default |
|----------|-------|------------|-----------------|
| **DES** (DF_KEY_DES) | **16** | 2K3DES (two-key) | **Yes** ✓ |
| **3K3DES** (DF_KEY_3K3DES) | **24** | 3K3DES (three-key) | No |
| **AES** (DF_KEY_AES) | **16** | AES-128 | No |

## Key Type Details

### DES (DF_KEY_DES = 0)
- **Actually**: 2K3DES (two-key Triple DES)
- **Length**: 16 bytes
- **Algorithm**: Triple DES with 2 keys
- **Use**: Legacy compatibility
- **Example**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

### 3K3DES (DF_KEY_3K3DES = 1)
- **Full name**: Three-key Triple DES
- **Length**: 24 bytes (3 × 8-byte keys)
- **Algorithm**: Triple DES with 3 keys
- **Use**: Enhanced security over 2K3DES
- **Example**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

### AES (DF_KEY_AES = 2)
- **Full name**: Advanced Encryption Standard
- **Length**: 16 bytes
- **Algorithm**: AES-128
- **Use**: **Recommended for new applications**
- **Example**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

## Why "DES" is Confusing

In the DESFire context:
- **"DES"** does NOT mean single DES (8 bytes)
- **"DES"** means 2K3DES (16 bytes)
- This is for backward compatibility naming

## Common Misconception

✅ **CORRECT**:
```
DES (2K3DES) = 16 bytes
3K3DES = 24 bytes
AES = 16 bytes
```

## Key Format

DESFire keys must be provided as hex bytes, space-separated:

**DES and AES (16 bytes):**
```
00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
```

**3K3DES (24 bytes):**
```
00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17
```

## Factory Default

**All factory DESFire cards come with:**
- **PICC Master Key**: DES (2K3DES), all zeros (16 bytes)
- **Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

## Choosing a Key Type

### For PICC Authentication
- **Factory cards**: Use 3K3DES (must match card's current setting)
- **After upgrade**: Use whatever you changed it to (typically AES)

### For New Applications
- **Recommended**: AES
  - Modern encryption
  - Better security
  - Industry standard

- **Legacy**: 3K3DES or DES
  - Use only if you need compatibility with old systems
  - DES (2K3DES) provides less security than 3K3DES

## Authentication Example

```csharp
// Factory card - authenticate with 3K3DES
await Authenticate(
    key: "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
    keyType: DF_KEY_3K3DES,  // Type 1
    keyNo: 0
);

// Then create AES application
await CreateApplication(
    appKeyType: DF_KEY_AES,  // Type 2 - recommended!
    maxKeys: 4,
    appId: 0x000001
);

// New app has AES keys (16 bytes each)
// Key 0: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
// Key 1: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
// ...etc
```

## Key Type vs Key Value

**Key Type** determines the encryption algorithm:
- DES (2K3DES)
- 3K3DES
- AES

**Key Value** is the actual 16-byte secret:
- Factory default: All zeros
- Production: Should be random/secure

Both are 16 bytes, but the algorithm differs!

## Summary

- ✅ DES (2K3DES): **16 bytes**
- ✅ 3K3DES: **24 bytes**
- ✅ AES: **16 bytes**
- ✅ Factory PICC: **DES** (type 0, 16 bytes)
- ✅ Recommended for new apps: **AES** (type 2, 16 bytes)
- ❌ "DES" ≠ 8 bytes (it means 2K3DES = 16 bytes)
