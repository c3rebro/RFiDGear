# File Access Keys - Important!

## The Issue: Access Denied Errors

If you get "Access Denied" when reading/writing files, it's because you're **authenticating with the wrong key**.

## How DESFire File Access Works

Each file has **4 access rights**, each specifying which key is required:

| Access Right | Purpose | Default in Sample |
|--------------|---------|-------------------|
| **Read** | Read file data | Key 1 |
| **Write** | Write file data | Key 1 |
| **ReadWrite** | Combined R/W | Key 1 |
| **Change** | Change file settings | Key 0 (master) |

## Key Numbers Explained

| Value | Meaning |
|-------|---------|
| `0-13` | Specific key number required (0 = master key) |
| `14` | **Free access** (no authentication needed) |
| `15` | **Never** (access blocked) |

## The Fix

The sample now **automatically authenticates with the correct key** based on file access rights:

### For Writing
```csharp
// File configured with Write = Key 1
// Sample authenticates with Key 1 (not Key 0!)
Authenticating with key 1 for write access...
```

### For Reading
```csharp
// File configured with Read = Key 1
// Sample authenticates with Key 1 (not Key 0!)
Authenticating with key 1 for read access...
```

## All Keys Start as All Zeros

When you create a new application, **all keys are initialized to all zeros**.

**For AES applications (16 bytes each):**
```
Key 0 (master): 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
Key 1:          00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
Key 2:          00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
Key 3:          00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

**For 3K3DES applications (24 bytes each):**
```
Key 0 (master): 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
Key 1:          00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

So the sample can authenticate with any key number using the default all-zeros value!

## Example Scenarios

### Scenario 1: File Requires Key 1 (Default)
```
File Access Rights:
  Read: Key 1
  Write: Key 1

Operation: Write data
  → Sample authenticates with Key 1 (all zeros)
  → Write succeeds ✓
```

### Scenario 2: File with Free Access
```
File Access Rights:
  Read: 14 (free)
  Write: 14 (free)

Operation: Write data
  → Sample authenticates with Key 0 (fallback)
  → Write succeeds ✓
```

### Scenario 3: File with Key 0 (Master Key)
```
File Access Rights:
  Read: Key 0
  Write: Key 0

Operation: Write data
  → Sample authenticates with Key 0 (all zeros)
  → Write succeeds ✓
```

### Scenario 4: File with "Never" Access ❌
```
File Access Rights:
  Write: 15 (never)

Operation: Write data
  → Sample detects write=15 and aborts
  → ERROR: File has write access set to NEVER (15)!
```

## Recommended File Access Rights

### For Testing (Easiest)
Use **free access** for read/write:
```
Read: 14 (free)
Write: 14 (free)
ReadWrite: 14 (free)
Change: 0 (master key only)
```

### For Production (Secure)
Use **specific keys**:
```
Read: Key 1
Write: Key 2
ReadWrite: Key 3
Change: Key 0 (master key only)
```

Then change the keys from default all-zeros to secure random values!

## Changed Keys?

If you've changed key values (not using default all-zeros), you need to update the sample code to use the new key values.

For example, if you changed Key 1 to `11 11 11...11`:
- The sample still tries to use `00 00 00...00` for Key 1
- Authentication fails ❌
- **Solution**: Either use default keys, or update `appConfig.AppMasterKey` to track all key values

## Master Key Special Powers?

**No!** Unlike some smart cards, in DESFire the master key (Key 0) does NOT automatically have access to all files.

Each file's access rights are **strictly enforced**:
- If file says "Read = Key 1", you MUST authenticate with Key 1
- Master key (Key 0) only has access if explicitly configured

## Quick Fix for Access Denied

If you get access denied:

1. **Check file access rights** - which key is required?
2. **Verify you're using default keys** - all zeros (16 bytes)
3. **Or use free access (14)** for testing - no auth required

Run the sample again and when creating the file:
```
Read access [default: 1]: 14
Write access [default: 1]: 14
```

This sets free access and bypasses the authentication requirement!
