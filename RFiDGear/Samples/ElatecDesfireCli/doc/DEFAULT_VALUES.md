# Default Sample Parameters

Press **Enter** at any prompt to use these default values for quick testing.

## ⚠️ CRITICAL: DESFire Key Lengths

**DESFire key types have different lengths:**
- DES (2K3DES) = 16 bytes
- 3K3DES = **24 bytes**
- AES = 16 bytes

**Factory DESFire cards use DES (2K3DES), NOT AES!**

- **PICC Master Key Type**: DES (0) - **FACTORY DEFAULT**
- **PICC Master Key Value**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes)

The sample defaults to 3K3DES for PICC authentication to work with factory-fresh cards.

See `DESFIRE_KEY_TYPES.md` for detailed explanation.

## Application Configuration

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **Application ID** | `0x000001` | Hex application identifier |
| **Number of Keys** | `4` | Keys 0-3 available |
| **App Key Type** | `AES` (type 2) | 16-byte AES keys for NEW app (recommended) |
| **Change Key Mode** | `0xE0` | Each key changes itself |
| **Allow Change MK** | `Yes` | Master key is changeable |
| **Allow Free Listing** | `Yes` | Can list files without auth |
| **Allow Create/Delete** | `No` | Requires master key |
| **Config Changeable** | `Yes` | Settings can be changed |
| **PICC Key Type** | `DES` (0) | 16-byte 2K3DES - **FACTORY DEFAULT** |
| **PICC Master Key** | All zeros (DES) | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes) |
| **App Master Key** | All zeros (AES) | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes) |

**Resulting Key Settings**: `0xEB` (0xE0 + 0x01 + 0x02 + 0x08)

### Understanding Key Types

**Two different key types are used:**

1. **PICC Key Type** (for authenticating to the card)
   - Factory default: **DES/2K3DES** (16 bytes)
   - Key: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
   - Used to: Authenticate to PICC (app 0) before creating applications
   - **Must match the current PICC configuration**

2. **Application Key Type** (for the new application being created)
   - Default: **AES** (16 bytes, recommended for new apps)
   - Keys: All zeros initially (16 bytes for AES, 24 bytes for 3K3DES)
   - Used for: New application's keys (including master key 0)
   - **Can be different from PICC key type**

**Important**: You authenticate with DES to the PICC, then create an AES application!

## PICC Master Key Change (Optional)

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **Current Key** | All zeros | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| **New Key** | Sequential | `01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10` |
| **Key Type** | AES | 16-byte AES |
| **Key Settings** | `0x0F` | Master key is changeable |

## Application Key Change (Optional)

With **0xE0 mode**, each key authenticates with itself to change:

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **Target Key Number** | `1` | Change key 1 (not master key 0) |
| **Current Value** | All zeros | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| **New Value** | All ones | `11 11 11 11 11 11 11 11 11 11 11 11 11 11 11 11` |
| **Auth Key** | Same as current | Authenticates with key 1's current value |

**Change Mode Behavior**:
- `0x00`: Always authenticate with key 0 (master key)
- `0xE0`: Authenticate with the target key itself ✓ (default)
- `0xF0`: Keys frozen, no changes allowed

## File Configuration

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **File Number** | `1` | File ID within application |
| **File Size** | `160` bytes | Matches taskdatabase.xml example |
| **Read Access** | Key `1` | Requires key 1 to read |
| **Write Access** | Key `1` | Requires key 1 to write |
| **Read+Write Access** | Key `1` | Requires key 1 for combined R/W |
| **Change Access** | Key `0` | Requires master key to change settings |

**Access Rights Encoding**: `0x0111`
- Read: 1 (Key 1)
- Write: 1 (Key 1)
- R/W: 1 (Key 1)
- Change: 0 (Key 0)

## Write Data

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **Data** | German greeting | 160 bytes from taskdatabase.xml |

**Default Text** (160 bytes):
```
" Ein gesundes neues Jahr allen Mitarbeitern...."
```

**Hex**:
```
20 45 69 6E 20 67 65 73 75 6E 64 65 73 20 6E 65 75 65 73 20 4A 61 68 72 20 61 6C 6C 65 6E 20 4D 69 74 61 72 62 65 69 74 65 72 6E 2C 20 6D 69 74 2D 47 6C 69 65 64 65 72 6E 20 75 6E 64 20 6D 69 74 2D 56 61 67 69 6E 65 6E 2E 20 4D 67 65 6E 20 61 6C 6C 65 20 65 75 72 65 20 57 75 65 6E 73 63 68 65 20 69 6E 20 45 72 66 75 65 6C 6C 75 6E 67 20 67 65 68 65 6E 2E 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

## Quick Test Workflow

To run a complete test with all defaults:

1. **Start**: `dotnet run` or `run.bat`
2. **Connect**: Automatic
3. **Search chip**: Press any key when chip is on reader
4. **Create app**: Press Enter 10 times (all defaults)
5. **Change PICC key**: Type `n` (skip) or press Enter 4 times
6. **Change app key**: Type `n` (skip) or press Enter 3 times
7. **Create file**: Press Enter 6 times (all defaults)
8. **Write data**: Press Enter (use default greeting)
9. **Read/verify**: Automatic
10. **Format tag**: Type `n` (skip) or type `FORMAT` to erase all data

**Total keystrokes with defaults**: ~26 (mostly Enter key!)

## Authentication Flow

With these defaults, here's how authentication works:

### 1. Create Application
- **Auth to**: PICC (app 0)
- **Key**: PICC master key (all zeros)
- **Creates**: Application 0x000001

### 2. Change App Key 1 (if selected)
- **Auth to**: Application 0x000001
- **Key**: App key 1 (current value: all zeros)
- **Changes**: Key 1 from 00s → 11s

### 3. Create File
- **Auth to**: Application 0x000001
- **Key**: App master key (key 0, all zeros)
- **Creates**: File 1 with access rights for key 1

### 4. Write/Read Data
- **Auth to**: Application 0x000001
- **Key**: App master key (key 0, all zeros)
- **Note**: Master key has access to all files

## Key Summary Table

After running with defaults and changing app key 1:

| Key | Location | Purpose | Initial Value | After Change |
|-----|----------|---------|---------------|--------------|
| PICC MK | Card level | Card format/apps | 00 00...00 | Same (or custom if changed) |
| App 0 | App 0x000001 | Application master | 00 00...00 | Unchanged |
| App 1 | App 0x000001 | File R/W operations | 00 00...00 | 11 11...11 (if changed) |
| App 2 | App 0x000001 | Available for use | 00 00...00 | Unchanged |
| App 3 | App 0x000001 | Available for use | 00 00...00 | Unchanged |

## Matching taskdatabase.xml

These defaults closely match the example workflow in `taskdatabase.xml`:
- ✓ App ID: 3060 (0x0BF4) in XML → 000001 in sample (easier to remember)
- ✓ 4 keys with AES encryption
- ✓ Change key mode: 0xE0
- ✓ File size: 160 bytes
- ✓ Access rights: Key 1 for R/W, Key 0 for change
- ✓ Same German greeting text for write operation

The main difference is the App ID - the sample uses `0x000001` which is easier to type and remember during testing.
