# Quick Start Guide

## Build and Run

## ⚡ Quick Testing with Defaults

**Press Enter at any prompt to use default values!**

The sample automatically detects your card type and runs the appropriate workflow.

### MIFARE Classic Defaults
- Sector: 1 (block 4)
- Key A/B: `FF FF FF FF FF FF` (factory default)
- Data: `Hello MIFARE!!!!` (16 bytes)

### DESFire Defaults
- Application: `0x000001`, 4 keys, AES, 0xE0 mode
- File: 160 bytes, Key 1 access
- Data: 160-byte German greeting

See `MIFARE_CLASSIC.md` and `DEFAULT_VALUES.md` for complete details.

## What This Sample Does

This CLI application demonstrates both MIFARE Classic and DESFire workflows:

### 1. Reader Connection
- Automatically detects the first available Elatec TWN4 reader
- Establishes connection and displays reader version
- Configures tag types for HF operation

### 2. Chip Detection & Identification
- Waits for you to place a MIFARE card on the reader
- Reads the chip UID
- Identifies chip type:
  - **MIFARE Classic**: 0x10-0x1F (1K/2K/4K)
  - **MIFARE Plus SL1**: 0x34-0x36 (Classic-compatible mode)
  - **DESFire**: 0x40-0x7F (EV0/EV1/EV2/EV3)
- Displays chip information and selects appropriate workflow

## MIFARE Classic Workflow

### 3a. Sector Configuration
- **Sector number**: 0-15 for 1K, 0-39 for 4K (default: 1)
- **Key A**: 6 bytes hex (default: `FF FF FF FF FF FF`)
- **Key B**: 6 bytes hex (default: `FF FF FF FF FF FF`)
- Block number automatically calculated

### 4a. Authenticate to Sector
- Authenticates using Key A
- Falls back to Key B if Key A fails
- Must succeed to proceed

### 5a. Write Data
- Enter 16 bytes hex data (default: `Hello MIFARE!!!!`)
- Writes to first data block of sector

### 6a. Read and Verify
- Re-authenticates to sector
- Reads back the written data
- Displays hex and ASCII format

## DESFire Workflow

### 3b. Application Creation
Interactive prompts for:
- **Application ID**: Hex value (e.g., `000001`)
- **Number of Keys**: 1-14 (typically 3-5)
- **Key Type**: DES (legacy), 3K3DES, or AES (recommended)
- **Change Key Mode**:
  - `0x00` = Master key changes all keys
  - `0xE0` = Each key changes itself (recommended)
  - `0xF0` = Keys frozen (no changes)
- **Key Settings**: Additional permissions (listing, create/delete, etc.)

### 4. PICC Master Key Change (Optional)
If you choose to change the card master key:
- Enter current PICC master key (default: all zeros)
- Enter new master key
- Select key type
- Set key settings

### 5. Application Key Change (Optional)
If you choose to change an application key:
- Select which key number to change
- Enter current and new key values
- Authentication is automatic based on change key mode

### 6. File Creation
Interactive prompts for:
- **File Number**: 0-31
- **File Size**: Size in bytes
- **Access Rights**: For Read, Write, Read+Write, Change operations
  - `0-13` = Key number required
  - `14` = Free access
  - `15` = Never/blocked

### 7. Data Writing
- Enter hex data to write (space-separated)
- Default suggestion: `68 65 6C 6C 6F 77 6F 72 6C 64` ("helloworld")
- Data is written to the created file

### 8. Data Verification
- Automatically reads back the written data
- Displays both hex and ASCII representations
- Confirms successful write operation

### 9. Format Tag (Optional)
- Resets the tag to factory defaults
- **WARNING**: Deletes ALL applications and files
- Requires typing 'FORMAT' to confirm
- Returns card to factory state (all keys reset to zeros)

## Quick Test Configuration

For a quick test run, use these values:

1. **Application**: `000001`, Keys: `3`, Type: `AES (2)`, Mode: `0xE0`, All Yes
2. **Skip PICC Key Change**: `n`
3. **Skip App Key Change**: `n`
4. **File**: Number: `0`, Size: `32`, All rights: `14` (free), Change: `0`
5. **Data**: Just press Enter for default "helloworld"

## Authentication Details

- **PICC (Card Level)**: Application ID `0x000000`, uses PICC master key
- **Application Level**: Uses application master key (key 0) or specific keys based on access rights
- **Auth Mode**: EV1 (`0x01`) for DESFire EV1/EV2/EV3 compatibility

## Default Keys

All factory-fresh DESFire cards have default keys (all zeros):
- **DES**: `00 00 00 00 00 00 00 00`
- **3K3DES/AES**: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`

## File Structure

```
Samples/ElatecDesfireCli/
├── ElatecDesfireCli.csproj    # Project file (.NET 8, Elatec.NET 0.6.1)
├── Program.cs                  # Main application code
├── README.md                   # Comprehensive documentation
├── EXAMPLES.md                 # Configuration examples and scenarios
├── QUICK_START.md             # This file
├── run.bat                     # Windows batch file to run easily
└── .gitignore                  # Git ignore for build artifacts
```

## Common Issues

### No Reader Found
- Check USB connection
- Verify driver installation
- Try unplugging and reconnecting the reader

### No Chip Detected
- Ensure the chip is properly placed on the reader
- Wait a moment and press any key when ready
- Try repositioning the chip

### Authentication Failed
- Using wrong key value (try default all-zeros key)
- Key type mismatch (ensure DES/3K3DES/AES matches)
- Application doesn't exist (create it first)

### Permission Denied
- Check access rights configuration
- Authenticate with the correct key number
- For testing, use free access (14) for all operations

## Next Steps

After running this sample successfully:

1. Review `EXAMPLES.md` for more complex scenarios
2. Try different key configurations and access rights
3. Experiment with multiple applications on one card
4. Implement similar operations in your own project using RFiDGear as reference

## Technical Reference

This sample is based on the production RFiDGear implementation:
- `/Infrastructure/ReaderProviders/ElatecNetProvider.cs` - Reader operations
- `/ViewModels/TaskSetupViewModels/MifareDesfireSetupViewModel.cs` - Application/file management
- `/Infrastructure/AccessControl/` - DESFire access control logic

## Support

For issues or questions:
- Check the [RFiDGear repository](https://github.com/c3rebro/RFiDGear)
- Review the [Elatec.NET package documentation](https://www.nuget.org/packages/Elatec.NET/)
- Consult the [DESFire EV1 datasheet](https://www.nxp.com/docs/en/data-sheet/MF3ICD41.pdf)
