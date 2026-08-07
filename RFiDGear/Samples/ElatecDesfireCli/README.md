# Elatec TWN4 MIFARE Sample CLI

A .NET 8 console application demonstrating both MIFARE Classic and DESFire operations using the Elatec.NET library (v0.6.1) and TWN4 reader.

## Features

This sample automatically detects the chip type and runs the appropriate workflow:

### MIFARE Classic Workflow (1K/2K/4K, Plus SL1)

1. **Connect to Reader** - Automatically detects and connects to Elatec TWN4 readers
2. **Chip Detection** - Identifies MIFARE Classic 1K/2K/4K or Plus SL1 cards
3. **Sector Authentication** - Authenticates to a sector using Key A or Key B
4. **Data Writing** - Writes 16 bytes to a block
5. **Data Reading** - Reads back and displays the written data for verification

### DESFire Workflow (EV0/EV1/EV2/EV3)

1. **Connect to Reader** - Automatically detects and connects to Elatec TWN4 readers
2. **Chip Detection** - Identifies DESFire EV0/EV1/EV2/EV3 cards
3. **Application Creation** - Creates a DESFire application with custom settings
4. **PICC Master Key Change** - Changes the card master key (optional)
5. **Application Key Change** - Changes application-level keys (optional)
6. **File Creation** - Creates a standard data file with access rights
7. **Data Writing** - Writes hex data to the file (default: 160-byte German greeting)
8. **Data Reading** - Reads back and displays the written data for verification
9. **Format Tag** - Resets the tag to factory defaults, erasing all data (optional)

## Prerequisites

- .NET 8 SDK
- Elatec TWN4 reader (USB or serial connection)
- **MIFARE Classic**: 1K, 2K, 4K, or Plus SL1 card/tag
- **DESFire**: EV0, EV1, EV2, or EV3 card/tag

## ⚠️ IMPORTANT: DESFire Key Types

**DESFire key lengths:** DES/AES=16 bytes, 3K3DES=24 bytes

**Factory DESFire cards use DES (2K3DES) keys, NOT AES:**
- PICC Master Key: DES (2K3DES), `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (16 bytes)
- The sample defaults to DES for PICC authentication
- You can create AES applications on factory cards

See `DESFIRE_KEY_TYPES.md` and `FACTORY_DEFAULTS.md` for details.

## Building the Project

```bash
cd Samples/ElatecDesfireCli
dotnet restore
dotnet build
```

## Running the Sample

```bash
dotnet run
```

## Usage Guide

The sample automatically detects whether you have a MIFARE Classic or DESFire chip and runs the appropriate workflow.

## MIFARE Classic Usage

### 1. Sector Configuration

When working with a MIFARE Classic card, you'll be prompted for:

- **Sector Number**: Which sector to access (0-15 for 1K, 0-39 for 4K)
  - Default: Sector 1
  - The sample automatically calculates the first data block of the sector
- **Key A**: 6-byte authentication key (hex, space-separated)
  - Default: `FF FF FF FF FF FF` (factory default)
- **Key B**: 6-byte authentication key (hex, space-separated)
  - Default: `FF FF FF FF FF FF` (factory default)

### 2. Block Calculation

The sample automatically calculates which block to write based on the sector:
- **Sectors 0-31**: 4 blocks each (blocks 0-127)
  - Sector 1 → Block 4 (first data block of sector 1)
- **Sectors 32-39**: 16 blocks each (blocks 128-255, only on 4K cards)

### 3. Data Writing

Enter 16 bytes of data to write:
- Default: `Hello MIFARE!!!!` (ASCII, 16 bytes)
- Format: Hex bytes space-separated (e.g., `48 65 6C 6C 6F 20 4D 49 46 41 52 45 21 21 21 21`)

### 4. MIFARE Classic Example Session

```
=== MIFARE Classic Workflow ===

--- Classic Configuration ---
Sector number (0-15 for 1K, 0-39 for 4K) [default: 1]:
Block number to write: 4 (first data block of sector 1)
Key A (6 bytes hex, space-separated) [default: FF FF FF FF FF FF]:
Key B (6 bytes hex, space-separated) [default: FF FF FF FF FF FF]:

--- Authenticate to Sector 1 ---
Authenticating to sector 1 with Key A...
Successfully authenticated to sector 1!

--- Write Data to Block 4 ---
Enter data to write (16 bytes hex, space-separated)
[default: 'Hello MIFARE!!!!' in ASCII]:
Writing 16 bytes to block 4...
  Data (hex): 48 65 6C 6C 6F 20 4D 49 46 41 52 45 21 21 21 21
  Data (ASCII): Hello MIFARE!!!!
Write successful!

--- Read Data from Block 4 ---
Re-authenticating to sector 1...
Reading block 4...
Read 16 bytes:
  Hex: 48 65 6C 6C 6F 20 4D 49 46 41 52 45 21 21 21 21
  ASCII: Hello MIFARE!!!!

=== MIFARE Classic workflow completed successfully! ===
```

## DESFire Usage

### 1. Application Creation

When prompted, you'll configure the DESFire application:

- **Application ID**: Hex value (e.g., `000001`)
- **Number of Keys**: 1-14 keys (typically 3-5)
- **Key Type**:
  - `0` = DES (8 bytes, legacy)
  - `1` = 3K3DES (16 bytes)
  - `2` = AES (16 bytes, **recommended**)
- **Change Key Mode**:
  - `0x00` = Only master key can change other keys
  - `0xE0` = Each key can change itself (**recommended**)
  - `0xF0` = Keys are frozen (no changes allowed)
- **Additional Settings**:
  - Allow changing master key
  - Allow free listing (no auth needed to list files)
  - Allow free create/delete (no auth needed)
  - Configuration changeable

### 2. PICC Master Key Change (Optional)

Change the card-level master key:

- **Current Key**: The existing PICC master key (default: all zeros)
- **New Key**: The new master key (hex, space-separated)
- **Key Type**: DES, 3K3DES, or AES
- **Key Settings**: Typically `0x0F` for changeable master key

### 3. Application Key Change (Optional)

Change an application-level key:

- **Target Key Number**: Which key to change (0 to max-1)
- **Current Value**: Current key value (default: all zeros)
- **New Value**: New key value (hex, space-separated)
- Authentication method is determined by the change key mode:
  - `0x00` mode: Requires master key (key 0)
  - `0xE0` mode: Requires the target key itself

### 4. File Creation

Configure the standard data file:

- **File Number**: 0-31 (unique within application)
- **File Size**: Size in bytes (e.g., 32, 64, 128)
- **Access Rights**: For each operation (Read, Write, Read+Write, Change):
  - `0-13` = Key number required
  - `14` = Free access (no authentication)
  - `15` = Never (operation blocked)

### 5. Data Writing

Write hex data to the file:

- Enter hex bytes space-separated (e.g., `48 65 6C 6C 6F`)
- Default: `68 65 6C 6C 6F 77 6F 72 6C 64` ("helloworld" in ASCII)
- Data length should not exceed the file size

### 6. Format Tag (Optional)

Reset the tag to factory defaults:

- **WARNING**: This operation deletes ALL applications and files
- **Confirmation**: You must type 'FORMAT' to confirm
- **Requirements**:
  - PICC master key must be correct
  - Authenticates to PICC (application 0)
- **Result**:
  - All applications deleted
  - All files deleted
  - PICC master key reset to factory default (all zeros, DES)
  - Card returned to factory state

**Use this to:**
- Clean up test data
- Prepare card for new workflow
- Reset a card to known state

## Example Session

```
=== Elatec TWN4 DESFire Sample CLI ===

--- Connecting to Elatec TWN4 Reader ---
Found reader: TWN4 Multitech 2 BLE
Connecting... Connected!
Reader Version: 1.2.3.4

--- Searching for chip...
Place a DESFire chip on the reader...
Press any key when ready...

Chip detected!
  UID: 04 12 34 56 78 90 AA
  Type: Mifare DESFire EV1
  Technology: HF

DESFire chip identified successfully!

--- Create DESFire Application ---
Enter application configuration:
Application ID (hex, e.g., 000001): 0x000001
Number of keys (1-14): 3
Key Type:
  0 = DES (8 bytes)
  1 = 3K3DES (16 bytes)
  2 = AES (16 bytes, recommended)
Select (0-2): 2
Change Key Mode:
  0x00 = Change keys with Master Key only
  0xE0 = Change keys with targeted key
  0xF0 = Keys frozen (no changes allowed)
Select change key mode (hex): 0xE0
Additional Key Settings:
Allow changing master key? (y/n): y
Allow free listing without master key? (y/n): y
Allow free create/delete without master key? (y/n): n
Configuration changeable? (y/n): y

Creating application 0x000001...
Authenticating to PICC...
Creating application...
Application 0x000001 created successfully!
  Max Keys: 3
  Key Type: DF_KEY_AES
  Key Settings: 0xEB

[... continues with remaining steps ...]
```

## Key Format

DESFire keys must be provided in hex format with space separators:

- **DES (2K3DES, 16 bytes)**: `00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F`
- **3K3DES (24 bytes)**: `00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17`
- **AES (16 bytes)**: `00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F`

**Note**: In DESFire, "DES" actually means 2K3DES (16 bytes), not single DES (8 bytes).

**Factory default keys:**
- DES/AES: All zeros (16 bytes): `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- 3K3DES: All zeros (24 bytes): `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

## Access Rights Format

Access rights are encoded as a 16-bit value with 4-bit fields:

```
| Change (4 bits) | R/W (4 bits) | Write (4 bits) | Read (4 bits) |
```

Example: `0xEEE0` means:
- Read: Key 0 required
- Write: Free access (E)
- Read+Write: Free access (E)
- Change: Free access (E)

## Common Issues

### Reader Not Found
- Ensure the TWN4 reader is connected via USB
- Check Device Manager for proper driver installation
- Try reconnecting the reader

### Authentication Failed
- Verify you're using the correct key value
- Ensure key type matches (DES/3K3DES/AES)
- Default factory key is all zeros

### Application Already Exists
- Delete the application first or use a different App ID
- Format the card to remove all applications (will erase all data!)

### File Creation Failed
- Ensure you're authenticated to the application
- Check that the file number is not already in use
- Verify the application has enough space

## Technical Details

### Authentication Mode
This sample uses **EV1 authentication mode** (`0x01`) which is compatible with:
- DESFire EV1
- DESFire EV2
- DESFire EV3

For legacy DESFire cards, you may need to use compatible mode (`0x00`).

### Communication Encryption
All file operations in this sample use **plain communication mode** (`CM_PLAIN`) for simplicity. For production use, consider:
- `CM_MAC`: MAC-protected communication
- `CM_ENCRYPT`: Fully encrypted communication

### Based on RFiDGear Implementation
This sample is derived from the production RFiDGear implementation:
- `ElatecNetProvider.cs`: Reader communication layer
- `MifareDesfireSetupViewModel.cs`: Application and file management
- `DesfireKeyChangeInputs.cs`: Key change logic

## References

- [Elatec.NET NuGet Package](https://www.nuget.org/packages/Elatec.NET/)
- [DESFire EV1 Documentation](https://www.nxp.com/docs/en/data-sheet/MF3ICD41.pdf)
- [RFiDGear Project](https://github.com/c3rebro/RFiDGear)

## License

This sample code is provided as-is for educational and development purposes.
