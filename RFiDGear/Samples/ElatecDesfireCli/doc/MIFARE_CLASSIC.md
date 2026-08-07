# MIFARE Classic Operations

## Overview

This document explains MIFARE Classic card operations in the sample CLI.

## Supported Cards

- **MIFARE Classic 1K** (0x11) - 16 sectors, 64 blocks
- **MIFARE Classic 2K** (0x12) - 32 sectors, 128 blocks
- **MIFARE Classic 4K** (0x13) - 40 sectors, 256 blocks
- **MIFARE Plus SL1 1K** (0x34) - Classic-compatible mode
- **MIFARE Plus SL1 2K** (0x35) - Classic-compatible mode
- **MIFARE Plus SL1 4K** (0x36) - Classic-compatible mode

## Card Structure

### MIFARE Classic 1K Structure

```
Sectors 0-15 (16 sectors)
Each sector has 4 blocks (16 bytes each)

Sector 0:
  Block 0: Manufacturer data (read-only)
  Block 1: Data block
  Block 2: Data block
  Block 3: Sector trailer (keys + access bits)

Sector 1:
  Block 4: Data block ← Sample writes here
  Block 5: Data block
  Block 6: Data block
  Block 7: Sector trailer (keys + access bits)

...and so on
```

### MIFARE Classic 4K Structure

```
Sectors 0-31: 4 blocks each (blocks 0-127)
Sectors 32-39: 16 blocks each (blocks 128-255)
```

## Authentication

### Key Types

Each sector has two keys:
- **Key A** (6 bytes) - Typically used for read/write
- **Key B** (6 bytes) - Can be used for additional security

### Factory Default Keys

Most MIFARE Classic cards ship with factory default keys:
```
Key A: FF FF FF FF FF FF
Key B: FF FF FF FF FF FF
```

### Authentication Process

1. Select the sector you want to access
2. Authenticate using Key A or Key B
3. Once authenticated, you can read/write any data block in that sector
4. Authentication remains valid until:
   - Card is removed from field
   - Authentication to a different sector
   - Reader context is reset

## Block Operations

### Data Block vs Sector Trailer

- **Data blocks**: Can be read/written after authentication
- **Sector trailer**: Contains keys and access bits
  - Block 3 for sectors 0-31
  - Block 15 for sectors 32-39 (4K cards)
  - **WARNING**: Writing incorrect data to sector trailer can brick the sector!

### Block Numbering

**MIFARE Classic 1K:**
```
Sector 0: Blocks 0-3
Sector 1: Blocks 4-7
Sector 2: Blocks 8-11
...
Sector 15: Blocks 60-63
```

**MIFARE Classic 4K:**
```
Sectors 0-31: Same as 1K (blocks 0-127)
Sector 32: Blocks 128-143 (16 blocks!)
Sector 33: Blocks 144-159
...
Sector 39: Blocks 240-255
```

## Sample Defaults

| Parameter | Default Value | Notes |
|-----------|---------------|-------|
| **Sector** | 1 | First user sector (Sector 0 contains manufacturer data) |
| **Block** | 4 | First data block of sector 1 |
| **Key A** | `FF FF FF FF FF FF` | Factory default |
| **Key B** | `FF FF FF FF FF FF` | Factory default |
| **Data** | `Hello MIFARE!!!!` | 16 ASCII characters |

## Workflow

### 1. Sector Selection

The sample prompts for a sector number (default: 1). It automatically calculates the first data block of that sector.

### 2. Authentication

```
Authenticate to sector with Key A
  ↓
If success → can access blocks (based on access bits)
If fail → try Key B
```

**Note**: Access bits determine which operations each key can perform.

### 3. Write Data

```
Try authenticating with Key A and write
  ↓
If write fails → Try Key B and write
  ↓
If successful → Note which key worked
```

**Common scenario**: Some cards use Key A for read and Key B for write.

The sample automatically tries both keys:
1. First attempts write with Key A
2. If that fails, re-authenticates with Key B and tries again
3. Reports which key succeeded

### 4. Read Data

```
Re-authenticate to sector (context may be invalidated)
  ↓
Try Key A first, fall back to Key B if needed
  ↓
Read 16 bytes from block
  ↓
Display hex and ASCII
```

## Access Bits

Access bits in the sector trailer control what operations are allowed on each block. The sample doesn't modify access bits but handles different configurations automatically.

### Common Access Bit Configurations

#### Factory Default (Transport Configuration)
```
Key A: Read + Write
Key B: Read + Write
```
Both keys can perform all operations. This is the default on new cards.

#### Typical Production Configuration
```
Key A: Read only
Key B: Write only
```
This separates read and write permissions for better security.

#### Read-Only Configuration
```
Key A: Read only
Key B: No access or Read only
```
Prevents accidental modification.

### How the Sample Handles Access Bits

The sample automatically tries both keys for operations:

**For Write:**
1. Try Key A + Write
2. If fails → Try Key B + Write
3. Report which key worked

**For Read:**
1. Try Key A + Read
2. If fails → Try Key B + Read

This ensures compatibility with different access bit configurations without needing to read or interpret the access bits.

### Modifying Access Bits

⚠️ **WARNING**: Writing incorrect access bits can permanently lock a sector!

The sample does NOT modify access bits for safety. If you need custom access control, you must:
1. Calculate the correct access bit values
2. Write to the sector trailer (block 3 or 15)
3. Verify immediately after writing

For production use, consider using dedicated MIFARE management tools to configure access bits.

## Common Issues

### Authentication Failed

**Causes:**
- Wrong key value
- Card has been programmed with custom keys
- Card is not a genuine MIFARE Classic

**Solutions:**
- Try default key: `FF FF FF FF FF FF`
- If card has custom keys, you need to know the actual keys
- Use tools like `mfoc` or `mfcuk` to recover keys (for testing only!)

### Write Failed

**Causes:**
- Not authenticated to the sector
- Trying to write to sector trailer without proper access
- Trying to write to block 0 (manufacturer block)

**Solutions:**
- Ensure authentication succeeded before writing
- Only write to data blocks (not sector trailers)
- Don't write to block 0

### Context Invalidated

**Cause:**
- TWN4 reader invalidates context between operations

**Solution:**
- Sample calls `SearchTagAsync()` before each operation
- Re-authenticate before each read/write

## Security Considerations

### Factory Defaults Are Insecure

Cards with factory default keys (`FF FF FF FF FF FF`) offer NO security. Anyone can read/write the card.

### Production Use

For production:
1. Change all keys from factory defaults
2. Configure appropriate access bits
3. Store keys securely
4. Consider using MIFARE DESFire for better security

### Key Management

- Never hardcode keys in source code (for production)
- Store keys in secure configuration
- Use different keys for different sectors/applications
- Consider using diversified keys (derived from UID)

## Comparison: Classic vs DESFire

| Feature | MIFARE Classic | DESFire |
|---------|---------------|---------|
| **Security** | Crypto-1 (broken) | 3DES/AES (secure) |
| **Memory** | Simple blocks | Applications + Files |
| **Keys** | 6 bytes | 8-24 bytes |
| **Access Control** | Access bits | Flexible file access rights |
| **Encryption** | Weak | Strong |
| **Use Case** | Legacy systems, simple storage | Secure applications, transit, payments |

## Sample Code Flow

```csharp
// 1. Search for chip
await _reader.SearchTagAsync();

// 2. Authenticate to sector
await _reader.MifareClassic_LoginAsync(
    keyA,           // "FF FF FF FF FF FF"
    0,              // Key type: 0=Key A, 1=Key B
    sectorNumber    // Sector 1
);

// 3. Write block
byte[] data = { 0x48, 0x65, 0x6C, ... }; // 16 bytes
await _reader.MifareClassic_WriteBlockAsync(data, blockNumber);

// 4. Re-authenticate (context invalidated)
await _reader.SearchTagAsync();
await _reader.MifareClassic_LoginAsync(keyA, 0, sectorNumber);

// 5. Read block
byte[] readData = await _reader.MifareClassic_ReadBlockAsync(blockNumber);
```

## References

- [MIFARE Classic 1K Datasheet](https://www.nxp.com/docs/en/data-sheet/MF1S50YYX_V1.pdf)
- [MIFARE Classic 4K Datasheet](https://www.nxp.com/docs/en/data-sheet/MF1S70YYX_V1.pdf)
- [MIFARE Plus Datasheet](https://www.nxp.com/docs/en/data-sheet/MF1PLUSx0y1.pdf)
- [Elatec.NET Library](https://www.nuget.org/packages/Elatec.NET/)

## Important Notes

⚠️ **Do NOT write to sector trailers** unless you know exactly what you're doing. Incorrect access bits can permanently lock a sector!

⚠️ **Block 0 is read-only** on most cards. Don't try to write to it.

⚠️ **Crypto-1 is broken**. MIFARE Classic should only be used for non-security-critical applications or legacy system compatibility.

✅ **For new projects**, use MIFARE DESFire for better security.
