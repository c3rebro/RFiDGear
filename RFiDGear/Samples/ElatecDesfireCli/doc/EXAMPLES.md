# Example Configurations

This document provides example configurations for common DESFire scenarios.

## Example 1: Simple Application with Free Access

Perfect for testing and development.

**Application Settings:**
- App ID: `0x000001`
- Number of Keys: `3`
- Key Type: `AES` (2)
- Change Key Mode: `0xE0` (each key can change itself)
- Allow change master key: `Yes`
- Allow free listing: `Yes`
- Allow free create/delete: `No`
- Config changeable: `Yes`

**File Settings:**
- File Number: `0`
- File Size: `32` bytes
- Read Access: `14` (free)
- Write Access: `14` (free)
- Read+Write Access: `14` (free)
- Change Access: `0` (master key required)

**Data to Write:**
```
68 65 6C 6C 6F 77 6F 72 6C 64
```
(ASCII: "helloworld")

---

## Example 2: Secure Application with Key-Protected Access

Production-ready configuration with proper access control.

**Application Settings:**
- App ID: `0xABCDEF`
- Number of Keys: `5`
- Key Type: `AES` (2)
- Change Key Mode: `0xE0` (each key changes itself)
- Allow change master key: `Yes`
- Allow free listing: `No`
- Allow free create/delete: `No`
- Config changeable: `Yes`
- PICC Master Key: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` (default)

**Change PICC Master Key:**
- Old Key: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- New Key: `01 23 45 67 89 AB CD EF FE DC BA 98 76 54 32 10`
- Key Type: `AES` (2)
- Key Settings: `0x0F` (changeable)

**Change Application Master Key (Key 0):**
- Target Key: `0`
- Current Key: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- New Key: `11 11 11 11 11 11 11 11 11 11 11 11 11 11 11 11`

**File Settings (Secure Storage):**
- File Number: `1`
- File Size: `64` bytes
- Read Access: `1` (requires key 1)
- Write Access: `2` (requires key 2)
- Read+Write Access: `3` (requires key 3)
- Change Access: `0` (requires master key)

**Data to Write:**
```
54 68 69 73 20 69 73 20 73 65 63 75 72 65 20 64 61 74 61 21
```
(ASCII: "This is secure data!")

---

## Example 3: Multi-Application Card

Organizing a DESFire card with multiple applications for different purposes.

### Application 1: User Data (App ID: 0x000001)

**Settings:**
- Number of Keys: `3`
- Key Type: `AES`
- Change Key Mode: `0xE0`
- All permissions: `Yes`

**File 0: User Profile (32 bytes)**
- Read: Free (14)
- Write: Key 1
- Change: Key 0

**File 1: Preferences (16 bytes)**
- Read: Free (14)
- Write: Key 1
- Change: Key 0

### Application 2: Access Control (App ID: 0x000002)

**Settings:**
- Number of Keys: `2`
- Key Type: `AES`
- Change Key Mode: `0x00` (master key only)
- Allow change master key: `No` (more secure)
- Other permissions: `No`

**File 0: Badge Data (8 bytes)**
- Read: Key 1
- Write: Never (15)
- Change: Key 0

**File 1: Access Log (128 bytes)**
- Read: Key 1
- Write: Key 1
- Change: Key 0

### Application 3: Counters (App ID: 0x000003)

**Settings:**
- Number of Keys: `2`
- Key Type: `3K3DES`
- Change Key Mode: `0xF0` (frozen - no key changes)

**File 0: Visit Counter (4 bytes)**
- Read: Free (14)
- Write: Key 1
- Change: Never (15)

---

## Example 4: Testing Key Change Modes

### Mode 1: Master Key Changes All (0x00)

**Application Settings:**
- Change Key Mode: `0x00`
- This means only key 0 can change any key (including itself)

**To change key 1:**
- Authenticate with: Key 0
- Target key: 1
- Result: Key 1 is changed

**To change key 0:**
- Authenticate with: Key 0
- Target key: 0
- Result: Key 0 is changed

### Mode 2: Each Key Changes Itself (0xE0)

**Application Settings:**
- Change Key Mode: `0xE0`
- Each key can only change itself

**To change key 1:**
- Authenticate with: Key 1 (using its current value)
- Target key: 1
- Result: Key 1 is changed to new value

**To change key 0 (master key):**
- Authenticate with: Key 0
- Target key: 0
- Result: Key 0 is changed (if "Allow change master key" is set)

### Mode 3: Keys Frozen (0xF0)

**Application Settings:**
- Change Key Mode: `0xF0`
- No key changes allowed (read-only)

**Any key change attempt:**
- Result: Operation fails/denied

---

## Example 5: Data Formats

### ASCII Text
```
Input: "Hello"
Hex: 48 65 6C 6C 6F
```

### Binary Counter (Little Endian)
```
Value: 12345
Hex: 39 30 00 00
```

### UID Storage
```
UID: 04 12 34 56 78 90 AA
Hex: 04 12 34 56 78 90 AA
```

### Timestamp (Unix epoch, 4 bytes, Little Endian)
```
Date: 2024-01-01 00:00:00
Hex: 00 6C 28 65
```

### JSON-like structure (as bytes)
```
Text: {"id":123}
Hex: 7B 22 69 64 22 3A 31 32 33 7D
```

---

## Key Size Reference

| Key Type | Size (bytes) | Example (all zeros) |
|----------|--------------|---------------------|
| DES | 8 | `00 00 00 00 00 00 00 00` |
| 3K3DES | 16 | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |
| AES | 16 | `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00` |

---

## Access Rights Encoding

Access rights are 4-bit values for each operation:

| Value | Meaning |
|-------|---------|
| 0-13 | Key number required (0 = master key) |
| 14 (0xE) | Free access (no authentication) |
| 15 (0xF) | Never (access denied) |

**Example Access Rights: 0xEEE0**
```
Binary: 1110 1110 1110 0000
        |    |    |    |
        |    |    |    +-- Read: Key 0
        |    |    +------- Write: Free (E)
        |    +------------ R/W: Free (E)
        +----------------- Change: Free (E)
```

**Example Access Rights: 0x0123**
```
Binary: 0000 0001 0010 0011
        |    |    |    |
        |    |    |    +-- Read: Key 3
        |    |    +------- Write: Key 2
        |    +------------ R/W: Key 1
        +----------------- Change: Key 0 (master)
```

---

## Quick Reference: Default Values

- **Factory Default PICC Master Key**: All zeros (length depends on key type)
- **Factory Default App Master Key**: All zeros (length depends on key type)
- **Default Auth Mode**: EV1 (0x01)
- **Default Key Version**: 0x00
- **Free Access Code**: 14 (0xE)
- **No Access Code**: 15 (0xF)
- **Master Key Number**: 0

---

## Troubleshooting Common Scenarios

### "Authentication Failed" on New Application
**Cause**: The application was created with default keys (all zeros).
**Solution**: Use all-zero key matching the key type:
- AES: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- 3K3DES: `00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00`
- DES: `00 00 00 00 00 00 00 00`

### "Permission Denied" on File Access
**Cause**: Access rights don't allow the operation.
**Solution**:
1. Check file access rights
2. Authenticate with the correct key number
3. Or use free access (14) during testing

### "Cannot Change Key"
**Cause**: Change key mode is frozen (0xF0) or wrong auth key.
**Solution**:
1. Check change key mode setting
2. For 0x00: authenticate with key 0
3. For 0xE0: authenticate with the target key

### "Application Already Exists"
**Cause**: App ID is already in use.
**Solution**:
1. Use a different App ID
2. Delete the existing application first
3. Format the card (erases everything!)
