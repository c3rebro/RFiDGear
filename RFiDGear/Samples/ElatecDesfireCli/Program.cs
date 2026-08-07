using Elatec.NET;
using Elatec.NET.Cards.Mifare;

namespace ElatecDesfireCli;

class Program
{
    private const byte DESFIRE_AUTHMODE_EV1 = 0x01;
    private const byte MIFARE_CLASSIC_KEYA = 0x00;
    private const byte MIFARE_CLASSIC_KEYB = 0x01;

    private static TWN4ReaderDevice? _reader;

    private enum ChipType
    {
        DESFire,
        MifareClassic,
        Unknown
    }

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Elatec TWN4 MIFARE Sample CLI ===");
        Console.WriteLine("TIP: Press Enter at any prompt to use default values");
        Console.WriteLine("NOTE: Supports MIFARE Classic (1K/2K/4K, Plus SL1) and DESFire (EV0/EV1/EV2/EV3)");
        Console.WriteLine("      Classic default: Key A/B = FF FF FF FF FF FF");
        Console.WriteLine("      DESFire default: PICC=DES (16 bytes), New App=AES (16 bytes), Mode=0xE0\n");

        try
        {
            // Step 1: Connect to reader
            if (!await ConnectToReaderAsync())
            {
                Console.WriteLine("Failed to connect to reader. Exiting.");
                return 1;
            }

            // Step 2: Search and identify chip
            Console.WriteLine("\n--- Searching for chip...");
            var chipType = await SearchAndIdentifyChipAsync();
            if (chipType == ChipType.Unknown)
            {
                Console.WriteLine("No supported chip found. Exiting.");
                return 1;
            }

            // Branch based on chip type
            if (chipType == ChipType.MifareClassic)
            {
                return await RunMifareClassicWorkflowAsync();
            }
            else // DESFire
            {
                return await RunDesfireWorkflowAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            if (_reader != null && _reader.IsConnected)
            {
                await _reader.DisconnectAsync();
                Console.WriteLine("\nReader disconnected.");
            }
        }
    }

    #region MIFARE Classic Workflow

    static async Task<int> RunMifareClassicWorkflowAsync()
    {
        try
        {
            Console.WriteLine("\n=== MIFARE Classic Workflow ===");

            // Step 1: Get configuration
            Console.WriteLine("\n--- Classic Configuration ---");
            var config = GetClassicConfig();

            // Step 2: Authenticate to sector
            Console.WriteLine($"\n--- Authenticate to Sector {config.Sector} ---");
            if (!await AuthenticateToClassicSectorAsync(config))
            {
                Console.WriteLine("Authentication failed.");
                return 1;
            }

            // Step 3: Write data
            Console.WriteLine($"\n--- Write Data to Block {config.BlockNumber} ---");
            var writeData = GetClassicWriteData();
            if (!await WriteClassicBlockAsync(config.BlockNumber, writeData, config))
            {
                Console.WriteLine("Write failed.");
                return 1;
            }

            // Step 4: Read back and verify
            Console.WriteLine($"\n--- Read Data from Block {config.BlockNumber} ---");
            if (!await ReadClassicBlockAsync(config))
            {
                Console.WriteLine("Read failed.");
                return 1;
            }

            Console.WriteLine("\n=== MIFARE Classic workflow completed successfully! ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError in Classic workflow: {ex.Message}");
            return 1;
        }
    }

    record ClassicConfig(
        byte Sector,
        byte BlockNumber,
        string KeyA,
        string KeyB
    );

    static ClassicConfig GetClassicConfig()
    {
        Console.WriteLine("=== Sector Configuration (Press Enter for defaults) ===");

        // Sector number
        Console.Write("Sector number (0-15 for 1K, 0-39 for 4K) [default: 1]: ");
        var sectorInput = Console.ReadLine()?.Trim();
        byte sector = string.IsNullOrWhiteSpace(sectorInput) ? (byte)1 : byte.Parse(sectorInput);

        // Calculate first data block of sector
        // Sectors 0-31: 4 blocks each (0-3, 4-7, 8-11, ...)
        // Sectors 32-39: 16 blocks each (for 4K cards)
        byte blockNumber;
        if (sector < 32)
        {
            blockNumber = (byte)(sector * 4); // First block of sector
        }
        else
        {
            blockNumber = (byte)(128 + (sector - 32) * 16); // First block of large sector
        }

        Console.WriteLine($"Block number to write: {blockNumber} (first data block of sector {sector})");

        // Key A
        Console.Write("Key A (6 bytes hex, space-separated) [default: FF FF FF FF FF FF]: ");
        var keyAInput = Console.ReadLine()?.Trim();
        string keyA = string.IsNullOrWhiteSpace(keyAInput) ? "FF FF FF FF FF FF" : keyAInput;

        // Key B
        Console.Write("Key B (6 bytes hex, space-separated) [default: FF FF FF FF FF FF]: ");
        var keyBInput = Console.ReadLine()?.Trim();
        string keyB = string.IsNullOrWhiteSpace(keyBInput) ? "FF FF FF FF FF FF" : keyBInput;

        return new ClassicConfig(sector, blockNumber, keyA, keyB);
    }

    static async Task<bool> AuthenticateToClassicSectorAsync(ClassicConfig config)
    {
        try
        {
            // Re-establish context
            await _reader!.SearchTagAsync();

            Console.WriteLine($"Authenticating to sector {config.Sector} with Key A...");
            await _reader.MifareClassic_LoginAsync(config.KeyA, MIFARE_CLASSIC_KEYA, config.Sector);

            Console.WriteLine($"Successfully authenticated to sector {config.Sector}!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            Console.WriteLine("Hint: Ensure you're using the correct key (default factory key is FF FF FF FF FF FF)");
            return false;
        }
    }

    static byte[] GetClassicWriteData()
    {
        Console.WriteLine("Enter data to write (16 bytes hex, space-separated)");
        Console.Write("[default: 'Hello MIFARE!!!!' in ASCII]: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            // Default: "Hello MIFARE!!!!" (16 bytes)
            return new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x4D, 0x49,
                               0x46, 0x41, 0x52, 0x45, 0x21, 0x21, 0x21, 0x21 };
        }

        var hexParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (hexParts.Length != 16)
        {
            Console.WriteLine($"Warning: Expected 16 bytes, got {hexParts.Length}. Padding/truncating.");
        }

        byte[] data = new byte[16];
        for (int i = 0; i < Math.Min(hexParts.Length, 16); i++)
        {
            data[i] = Convert.ToByte(hexParts[i], 16);
        }

        return data;
    }

    static async Task<bool> WriteClassicBlockAsync(byte blockNumber, byte[] data, ClassicConfig config)
    {
        Console.WriteLine($"Writing {data.Length} bytes to block {blockNumber}...");
        Console.WriteLine($"  Data (hex): {BitConverter.ToString(data).Replace("-", " ")}");
        Console.WriteLine($"  Data (ASCII): {System.Text.Encoding.ASCII.GetString(data)}");

        // Try with Key A first
        try
        {
            // Re-establish context and authenticate with Key A
            await _reader!.SearchTagAsync();
            Console.WriteLine($"  Re-authenticating with Key A for write...");
            await _reader.MifareClassic_LoginAsync(config.KeyA, MIFARE_CLASSIC_KEYA, config.Sector);
            await _reader.MifareClassic_WriteBlockAsync(data, blockNumber);

            Console.WriteLine("Write successful with Key A!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Write with Key A failed: {ex.Message}");
        }

        // Try with Key B
        try
        {
            await _reader!.SearchTagAsync();
            Console.WriteLine($"  Trying Key B for write...");
            await _reader.MifareClassic_LoginAsync(config.KeyB, MIFARE_CLASSIC_KEYB, config.Sector);
            await _reader.MifareClassic_WriteBlockAsync(data, blockNumber);

            Console.WriteLine("Write successful with Key B!");
            Console.WriteLine("  Note: This sector uses Key A for read, Key B for write.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Write with Key B also failed: {ex.Message}");
            Console.WriteLine("  Hint: Check sector access bits configuration.");
            return false;
        }
    }

    static async Task<bool> ReadClassicBlockAsync(ClassicConfig config)
    {
        try
        {
            // Re-establish context and re-authenticate
            await _reader!.SearchTagAsync();

            Console.WriteLine($"Re-authenticating to sector {config.Sector}...");
            await _reader.MifareClassic_LoginAsync(config.KeyA, MIFARE_CLASSIC_KEYA, config.Sector);

            Console.WriteLine($"Reading block {config.BlockNumber}...");
            var readData = await _reader.MifareClassic_ReadBlockAsync(config.BlockNumber);

            if (readData != null && readData.Length > 0)
            {
                Console.WriteLine($"Read {readData.Length} bytes:");
                Console.WriteLine($"  Hex: {BitConverter.ToString(readData).Replace("-", " ")}");
                Console.WriteLine($"  ASCII: {System.Text.Encoding.ASCII.GetString(readData)}");
                return true;
            }
            else
            {
                Console.WriteLine("No data read from block.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Read failed: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region DESFire Workflow

    static async Task<int> RunDesfireWorkflowAsync()
    {
        try
        {
            Console.WriteLine("\n=== DESFire Workflow ===");

            // Step 3: Create Application
            Console.WriteLine("\n--- Create DESFire Application ---");
            var appConfig = GetApplicationConfig();
            if (!await CreateApplicationAsync(appConfig))
            {
                Console.WriteLine("Failed to create application.");
                return 1;
            }

            // Step 4: Change PICC Master Key
            Console.WriteLine("\n--- Change PICC Master Key ---");
            if (PromptYesNo("Do you want to change the PICC Master Key?"))
            {
                var piccKeyConfig = GetPiccMasterKeyConfig();
                if (!await ChangePiccMasterKeyAsync(piccKeyConfig))
                {
                    Console.WriteLine("Failed to change PICC master key.");
                    return 1;
                }
            }

            // Step 5: Change Application Key
            Console.WriteLine("\n--- Change Application Key ---");
            if (PromptYesNo("Do you want to change an application key?"))
            {
                var appKeyConfig = GetAppKeyConfig(appConfig);
                if (!await ChangeApplicationKeyAsync(appKeyConfig, appConfig.AppId))
                {
                    Console.WriteLine("Failed to change application key.");
                    return 1;
                }
            }

            // Step 6: Create File
            Console.WriteLine("\n--- Create Standard Data File ---");
            var fileConfig = GetFileConfig();
            if (!await CreateStdDataFileAsync(fileConfig, appConfig))
            {
                Console.WriteLine("Failed to create file.");
                return 1;
            }

            // Step 7: Write Data
            Console.WriteLine("\n--- Write Data to File ---");
            var writeData = GetWriteData();
            if (!await WriteDataToFileAsync(writeData, fileConfig, appConfig))
            {
                Console.WriteLine("Failed to write data.");
                return 1;
            }

            // Step 8: Read back and verify
            Console.WriteLine("\n--- Reading back data for verification ---");
            await ReadDataFromFileAsync(fileConfig, appConfig);

            // Step 9: Format Tag (Optional)
            Console.WriteLine("\n--- Format Tag ---");
            if (PromptYesNo("Do you want to format the tag? (This will erase ALL data!)", defaultValue: false))
            {
                if (!await FormatTagAsync(appConfig))
                {
                    Console.WriteLine("Failed to format tag.");
                    return 1;
                }
            }

            Console.WriteLine("\n=== DESFire workflow completed successfully! ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError in DESFire workflow: {ex.Message}");
            return 1;
        }
    }

    #endregion

    #region Step 1: Connect to Reader

    static async Task<bool> ConnectToReaderAsync()
    {
        Console.WriteLine("--- Connecting to Elatec TWN4 Reader ---");

        _reader = TWN4ReaderDevice.Instance.FirstOrDefault();

        if (_reader == null)
        {
            Console.WriteLine("No Elatec TWN4 reader found.");
            return false;
        }

        Console.WriteLine($"Found reader: Elatec TWN4");

        if (!_reader.IsConnected)
        {
            Console.Write("Connecting... ");
            var connected = await _reader.ConnectAsync();

            if (!connected)
            {
                Console.WriteLine("Failed!");
                return false;
            }

            Console.WriteLine("Connected!");
        }
        else
        {
            Console.WriteLine("Already connected.");
        }

        var version = await _reader.GetVersionStringAsync();
        Console.WriteLine($"Reader Version: {version}");

        // Set tag types to support all HF tags (including DESFire)
        await _reader.SetTagTypesAsync(LFTagTypes.NOTAG, HFTagTypes.AllHFTags);
        Console.WriteLine("Tag types configured for all HF tags.");

        return true;
    }

    #endregion

    #region Step 2: Search and Identify Chip

    static async Task<ChipType> SearchAndIdentifyChipAsync()
    {
        Console.WriteLine("Place a MIFARE chip on the reader...");
        Console.WriteLine("Press any key when ready...");
        Console.ReadKey(true);

        // always search for tags before talking to a chip to establish a context. context is invalidated after:
        // tag remove (power down) or failed login.
        // note: some readers invalidate even after a single command is performed, so it's good practice to search before every operation in a real application.
        await _reader!.SearchTagAsync();

        var tag = await _reader.GetSingleChipAsync();

        if (tag == null)
        {
            Console.WriteLine("No chip detected.");
            return ChipType.Unknown;
        }

        Console.WriteLine($"\nChip detected!");
        Console.WriteLine($"  UID: {tag.UIDHexString}");
        Console.WriteLine($"  ChipType: {tag.ChipType}");

        if (!(tag is MifareChip mifareChip))
        {
            Console.WriteLine($"\nWarning: This is not a MIFARE chip!");
            return ChipType.Unknown;
        }

        byte subTypeByte = (byte)mifareChip.SubType;
        Console.WriteLine($"  Mifare SubType: 0x{subTypeByte:X2} ({mifareChip.SubType})");

        // MIFARE Classic: 0x10 - 0x1F
        // MIFARE Plus SL1: 0x34 - 0x36
        // DESFire: 0x40 - 0x7F
        bool isClassic = (subTypeByte >= 0x10 && subTypeByte <= 0x1F);
        bool isPlusSL1 = (subTypeByte >= 0x34 && subTypeByte <= 0x36);
        bool isDESFire = (subTypeByte >= 0x40 && subTypeByte <= 0x7F);

        if (isClassic || isPlusSL1)
        {
            string variant = "";
            if (subTypeByte == 0x11) variant = "MIFARE Classic 1K";
            else if (subTypeByte == 0x12) variant = "MIFARE Classic 2K";
            else if (subTypeByte == 0x13) variant = "MIFARE Classic 4K";
            else if (subTypeByte == 0x34) variant = "MIFARE Plus SL1 1K";
            else if (subTypeByte == 0x35) variant = "MIFARE Plus SL1 2K";
            else if (subTypeByte == 0x36) variant = "MIFARE Plus SL1 4K";
            else variant = "MIFARE Classic/Plus";

            Console.WriteLine($"\n{variant} chip identified successfully!");
            Console.WriteLine("Will use MIFARE Classic workflow (authenticate, write, read).");
            return ChipType.MifareClassic;
        }
        else if (isDESFire)
        {
            string variant = "";
            if (subTypeByte >= 0x70 && subTypeByte <= 0x7F)
                variant = "DESFire EV3";
            else if (subTypeByte >= 0x60 && subTypeByte <= 0x6F)
                variant = "DESFire EV2";
            else if (subTypeByte >= 0x50 && subTypeByte <= 0x5F)
                variant = "DESFire EV1";
            else if (subTypeByte >= 0x40 && subTypeByte <= 0x4F)
                variant = "DESFire EV0";

            // Check if SmartMX variant (bit 3 set)
            if ((subTypeByte & 0x08) != 0)
                variant = "SmartMX " + variant;

            Console.WriteLine($"\n{variant} chip identified successfully!");
            Console.WriteLine("Will use DESFire workflow (create app, create file, write, read).");
            return ChipType.DESFire;
        }

        Console.WriteLine($"\nWarning: Unsupported chip type!");
        return ChipType.Unknown;
    }

    #endregion

    #region Step 3: Create Application

    record ApplicationConfig(
        uint AppId,
        byte MaxKeys,
        DESFireKeyType AppKeyType,         // Key type for the NEW application (can be AES)
        byte KeySettings,
        string PiccMasterKey,
        DESFireKeyType PiccKeyType,        // Key type for PICC auth (DES on factory cards!)
        string AppMasterKey                 // App master key (key 0), length matches AppKeyType
    );

    static ApplicationConfig GetApplicationConfig()
    {
        Console.WriteLine("\n=== Application Configuration (Press Enter for defaults) ===");

        // App Number
        Console.Write("Application ID (hex) [default: 000001]: 0x");
        var appIdInput = Console.ReadLine()?.Trim();
        var appIdHex = string.IsNullOrWhiteSpace(appIdInput) ? "000001" : appIdInput;
        var appId = Convert.ToUInt32(appIdHex, 16);

        // Number of keys
        Console.Write("Number of keys (1-14) [default: 4]: ");
        var maxKeysInput = Console.ReadLine()?.Trim();
        var maxKeys = string.IsNullOrWhiteSpace(maxKeysInput) ? (byte)4 : byte.Parse(maxKeysInput);
        if (maxKeys < 1 || maxKeys > 14)
        {
            Console.WriteLine("Invalid number of keys. Using default: 4");
            maxKeys = 4;
        }

        // Application Key Type (for the NEW application being created)
        Console.WriteLine("\nApplication Key Type (for new application):");
        Console.WriteLine("  0 = DES (2K3DES, 16 bytes)");
        Console.WriteLine("  1 = 3K3DES (24 bytes)");
        Console.WriteLine("  2 = AES (16 bytes, recommended)");
        Console.Write("Select (0-2) [default: 2 (AES)]: ");
        var appKeyTypeInput = Console.ReadLine()?.Trim();
        var appKeyTypeChoice = string.IsNullOrWhiteSpace(appKeyTypeInput) ? "2" : appKeyTypeInput;
        var appKeyType = appKeyTypeChoice switch
        {
            "0" => DESFireKeyType.DF_KEY_DES,
            "1" => DESFireKeyType.DF_KEY_3K3DES,
            _ => DESFireKeyType.DF_KEY_AES
        };

        // Key settings (change key mode)
        Console.WriteLine("\nChange Key Mode:");
        Console.WriteLine("  0x00 = Change keys with Master Key only");
        Console.WriteLine("  0xE0 = Change keys with targeted key (recommended)");
        Console.WriteLine("  0xF0 = Keys frozen (no changes allowed)");
        Console.Write("Select change key mode (hex) [default: E0]: 0x");
        var changeKeyModeInput = Console.ReadLine()?.Trim();
        var changeKeyMode = string.IsNullOrWhiteSpace(changeKeyModeInput) ? "E0" : changeKeyModeInput;
        var keySettingsBase = Convert.ToByte(changeKeyMode, 16);

        // Additional settings - with defaults
        Console.WriteLine("\nAdditional Key Settings (y/n, default: y for all):");
        var allowChangeMK = PromptYesNo("Allow changing master key?", true);
        var allowListing = PromptYesNo("Allow free listing without master key?", true);
        var allowCreateDel = PromptYesNo("Allow free create/delete without master key?", false);
        var configChangeable = PromptYesNo("Configuration changeable?", true);

        byte keySettings = keySettingsBase;
        if (allowChangeMK) keySettings |= 0x01;
        if (allowListing) keySettings |= 0x02;
        if (allowCreateDel) keySettings |= 0x04;
        if (configChangeable) keySettings |= 0x08;

        // PICC Master Key Type (for authentication to PICC)
        Console.WriteLine("\n--- PICC Authentication (for card-level access) ---");
        Console.WriteLine("Factory DESFire cards use DES/2K3DES (16 bytes)");
        Console.WriteLine("\nPICC Master Key Type:");
        Console.WriteLine("  0 = DES (2K3DES, 16 bytes) - FACTORY DEFAULT");
        Console.WriteLine("  1 = 3K3DES (24 bytes)");
        Console.WriteLine("  2 = AES (16 bytes)");
        Console.Write("Select (0-2) [default: 0 (DES/2K3DES)]: ");
        var piccKeyTypeInput = Console.ReadLine()?.Trim();
        var piccKeyTypeChoice = string.IsNullOrWhiteSpace(piccKeyTypeInput) ? "0" : piccKeyTypeInput;
        var piccKeyType = piccKeyTypeChoice switch
        {
            "1" => DESFireKeyType.DF_KEY_3K3DES,
            "2" => DESFireKeyType.DF_KEY_AES,
            _ => DESFireKeyType.DF_KEY_DES
        };

        // PICC Master Key for authentication
        Console.Write($"PICC Master Key (hex, space-separated) [default: all zeros]: ");
        var piccKeyInput = Console.ReadLine()?.Trim();
        string piccKey;
        if (string.IsNullOrWhiteSpace(piccKeyInput))
        {
            // Key length depends on type: DES/AES=16, 3K3DES=24
            piccKey = piccKeyType switch
            {
                DESFireKeyType.DF_KEY_3K3DES => "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                _ => "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
            };
        }
        else
        {
            piccKey = piccKeyInput;
        }

        // Set app master key (key 0 of new application) - length depends on type
        string appMasterKey = appKeyType switch
        {
            DESFireKeyType.DF_KEY_3K3DES => "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
            _ => "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
        };

        var piccKeyLength = piccKey.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var appKeyLength = appMasterKey.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        Console.WriteLine($"\n✓ App ID: 0x{appId:X6}, Keys: {maxKeys}, App Type: {appKeyType}, Mode: 0x{changeKeyMode}");
        Console.WriteLine($"✓ PICC Auth: {piccKeyType} ({piccKeyLength} bytes)");
        Console.WriteLine($"✓ App Keys: {appKeyType} ({appKeyLength} bytes, default all zeros)");
        return new ApplicationConfig(appId, maxKeys, appKeyType, keySettings, piccKey, piccKeyType, appMasterKey);
    }

    static async Task<bool> CreateApplicationAsync(ApplicationConfig config)
    {
        try
        {
            Console.WriteLine($"\nCreating application 0x{config.AppId:X6}...");

            // First authenticate to PICC (app 0) using PICC key type
            Console.WriteLine($"Authenticating to PICC with {config.PiccKeyType} key...");
            await _reader!.SearchTagAsync();
            await _reader!.MifareDesfire_SelectApplicationAsync(0);
            await _reader.MifareDesfire_AuthenticateAsync(
                config.PiccMasterKey,
                0, // Key number 0 (PICC master key)
                (byte)config.PiccKeyType, // Use PICC key type (DES for factory cards!)
                DESFIRE_AUTHMODE_EV1 // Mode 1 for EV1/EV2/EV3 chips
            );

            // Create application with specified app key type
            Console.WriteLine($"Creating application with {config.AppKeyType} keys...");
            await _reader.MifareDesfire_CreateApplicationAsync(
                (DESFireAppAccessRights)config.KeySettings,
                config.AppKeyType, // Use app key type (can be AES)
                config.MaxKeys,
                (int)config.AppId
            );

            Console.WriteLine($"Application 0x{config.AppId:X6} created successfully!");
            Console.WriteLine($"  Max Keys: {config.MaxKeys}");
            Console.WriteLine($"  App Key Type: {config.AppKeyType}");
            Console.WriteLine($"  Key Settings: 0x{config.KeySettings:X2}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating application: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Step 4: Change PICC Master Key

    record PiccKeyConfig(
        string OldKey,
        string NewKey,
        DESFireKeyType KeyType,
        byte NewKeySettings
    );

    static PiccKeyConfig GetPiccMasterKeyConfig()
    {
        Console.WriteLine("\n=== PICC Master Key Change (Press Enter for defaults) ===");

        Console.Write("Current PICC Master Key [default: all zeros]: ");
        var oldKeyInput = Console.ReadLine()?.Trim();
        var oldKey = string.IsNullOrWhiteSpace(oldKeyInput)
            ? "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
            : oldKeyInput;

        Console.Write("New PICC Master Key [default: 01 02 03...0F]: ");
        var newKeyInput = Console.ReadLine()?.Trim();
        var newKey = string.IsNullOrWhiteSpace(newKeyInput)
            ? "01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10"
            : newKeyInput;

        Console.WriteLine("\nNew Key Type:");
        Console.WriteLine("  0 = DES (2K3DES, 16 bytes)");
        Console.WriteLine("  1 = 3K3DES (24 bytes)");
        Console.WriteLine("  2 = AES (16 bytes)");
        Console.Write("Select [default: 2 (AES)]: ");
        var keyTypeInput = Console.ReadLine()?.Trim();
        var keyTypeChoice = string.IsNullOrWhiteSpace(keyTypeInput) ? "2" : keyTypeInput;
        var keyType = keyTypeChoice switch
        {
            "0" => DESFireKeyType.DF_KEY_DES,
            "1" => DESFireKeyType.DF_KEY_3K3DES,
            _ => DESFireKeyType.DF_KEY_AES
        };

        Console.WriteLine("\nNew Key Settings:");
        Console.WriteLine("  0x0F = Master key is changeable");
        Console.WriteLine("  0x00 = Master key not changeable without current master key");
        Console.Write("Select key settings (hex) [default: 0F]: 0x");
        var keySettingsInput = Console.ReadLine()?.Trim();
        var keySettings = string.IsNullOrWhiteSpace(keySettingsInput)
            ? (byte)0x0F
            : Convert.ToByte(keySettingsInput, 16);

        Console.WriteLine($"✓ Will change PICC key from default to custom key");
        return new PiccKeyConfig(oldKey, newKey, keyType, keySettings);
    }

    static async Task<bool> ChangePiccMasterKeyAsync(PiccKeyConfig config)
    {
        try
        {
            Console.WriteLine("\nChanging PICC Master Key...");

            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Authenticate to PICC
            await _reader.MifareDesfire_SelectApplicationAsync(0);
            await _reader.MifareDesfire_AuthenticateAsync(
                config.OldKey,
                0,
                (byte)config.KeyType,
                DESFIRE_AUTHMODE_EV1
            );

            // Change the key (version 0 for PICC master key)
            await _reader.MifareDesfire_ChangeKeyAsync(
                config.OldKey,
                config.NewKey,
                0, // Key version
                config.NewKeySettings,
                0, // Target key number (0 = master key)
                1, // Key count for PICC (always 1)
                config.KeyType
            );

            Console.WriteLine("PICC Master Key changed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing PICC master key: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Step 5: Change Application Key

    record AppKeyConfig(
        byte TargetKeyNo,
        string CurrentKey,
        string NewKey,
        DESFireKeyType KeyType,
        string AuthKey,
        byte AuthKeyNo
    );

    static AppKeyConfig GetAppKeyConfig(ApplicationConfig appConfig)
    {
        Console.WriteLine("\n=== Application Key Change (Press Enter for defaults) ===");

        Console.Write($"Target key number to change (0-{appConfig.MaxKeys - 1}) [default: 1]: ");
        var targetKeyNoInput = Console.ReadLine()?.Trim();
        var targetKeyNo = string.IsNullOrWhiteSpace(targetKeyNoInput) ? (byte)1 : byte.Parse(targetKeyNoInput);

        Console.Write("Current value of target key [default: all zeros]: ");
        var currentKeyInput = Console.ReadLine()?.Trim();
        var currentKey = string.IsNullOrWhiteSpace(currentKeyInput)
            ? "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
            : currentKeyInput;

        Console.Write("New value for target key [default: all 11s]: ");
        var newKeyInput = Console.ReadLine()?.Trim();
        var newKey = string.IsNullOrWhiteSpace(newKeyInput)
            ? "11 11 11 11 11 11 11 11 11 11 11 11 11 11 11 11"
            : newKeyInput;

        // Determine authentication key based on change key mode
        byte authKeyNo;
        string authKey;

        var changeKeyMode = (byte)(appConfig.KeySettings & 0xF0);

        if (changeKeyMode == 0x00) // ChangeKeyWithMasterKey
        {
            Console.WriteLine("\nChange key mode (0x00) requires authentication with app master key (key 0).");
            authKeyNo = 0;
            Console.Write("Application master key (key 0) for auth [default: all zeros]: ");
            var authKeyInput = Console.ReadLine()?.Trim();
            authKey = string.IsNullOrWhiteSpace(authKeyInput)
                ? "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
                : authKeyInput;
        }
        else if (changeKeyMode == 0xE0) // ChangeKeyWithTargetedKeyNumber
        {
            Console.WriteLine($"\nChange key mode (0xE0) requires authentication with target key {targetKeyNo}.");
            authKeyNo = targetKeyNo;
            authKey = currentKey; // Authenticate with the current value
            Console.WriteLine($"Will authenticate with current key value.");
        }
        else
        {
            Console.WriteLine($"\nWarning: Change key mode 0x{changeKeyMode:X2} detected. Attempting with master key...");
            authKeyNo = 0;
            Console.Write("Application master key (key 0) for auth [default: all zeros]: ");
            var authKeyInput = Console.ReadLine()?.Trim();
            authKey = string.IsNullOrWhiteSpace(authKeyInput)
                ? "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
                : authKeyInput;
        }

        Console.WriteLine($"✓ Will change key {targetKeyNo}: 00s → 11s");
        return new AppKeyConfig(targetKeyNo, currentKey, newKey, appConfig.AppKeyType, authKey, authKeyNo);
    }

    static async Task<bool> ChangeApplicationKeyAsync(AppKeyConfig config, uint appId)
    {
        try
        {
            Console.WriteLine($"\nChanging application key {config.TargetKeyNo}...");

            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Select and authenticate to application
            await _reader.MifareDesfire_SelectApplicationAsync(appId);
            await _reader.MifareDesfire_AuthenticateAsync(
                config.AuthKey,
                config.AuthKeyNo,
                (byte)config.KeyType,
                DESFIRE_AUTHMODE_EV1
            );

            // Change the key
            await _reader.MifareDesfire_ChangeKeyAsync(
                config.CurrentKey,
                config.NewKey,
                0, // Key version
                0, // Key settings (not used for app keys)
                config.TargetKeyNo,
                1, // Key count
                config.KeyType
            );

            Console.WriteLine($"Application key {config.TargetKeyNo} changed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error changing application key: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Step 6: Create File

    record FileConfig(
        byte FileNo,
        uint FileSize,
        byte ReadKey,
        byte WriteKey,
        byte ReadWriteKey,
        byte ChangeKey
    );

    static FileConfig GetFileConfig()
    {
        Console.WriteLine("\n=== Standard Data File Configuration (Press Enter for defaults) ===");

        Console.Write("File number (0-31) [default: 1]: ");
        var fileNoInput = Console.ReadLine()?.Trim();
        var fileNo = string.IsNullOrWhiteSpace(fileNoInput) ? (byte)1 : byte.Parse(fileNoInput);

        Console.Write("File size in bytes [default: 160]: ");
        var fileSizeInput = Console.ReadLine()?.Trim();
        var fileSize = string.IsNullOrWhiteSpace(fileSizeInput) ? 160u : uint.Parse(fileSizeInput);

        Console.WriteLine("\nFile Access Rights (0-13 = key number, 14 = free, 15 = never):");
        Console.WriteLine("[Default: Key 1 for all operations, Key 0 for change]");

        Console.Write("Read access [default: 1]: ");
        var readKeyInput = Console.ReadLine()?.Trim();
        var readKey = string.IsNullOrWhiteSpace(readKeyInput) ? (byte)1 : byte.Parse(readKeyInput);

        Console.Write("Write access [default: 1]: ");
        var writeKeyInput = Console.ReadLine()?.Trim();
        var writeKey = string.IsNullOrWhiteSpace(writeKeyInput) ? (byte)1 : byte.Parse(writeKeyInput);

        Console.Write("Read+Write access [default: 1]: ");
        var readWriteKeyInput = Console.ReadLine()?.Trim();
        var readWriteKey = string.IsNullOrWhiteSpace(readWriteKeyInput) ? (byte)1 : byte.Parse(readWriteKeyInput);

        Console.Write("Change access [default: 0]: ");
        var changeKeyInput = Console.ReadLine()?.Trim();
        var changeKey = string.IsNullOrWhiteSpace(changeKeyInput) ? (byte)0 : byte.Parse(changeKeyInput);

        Console.WriteLine($"✓ File {fileNo}: {fileSize} bytes, R/W=Key{readKey}/{writeKey}, Change=Key{changeKey}");
        return new FileConfig(fileNo, fileSize, readKey, writeKey, readWriteKey, changeKey);
    }

    static async Task<bool> CreateStdDataFileAsync(FileConfig fileConfig, ApplicationConfig appConfig)
    {
        try
        {
            Console.WriteLine($"\nCreating standard data file {fileConfig.FileNo}...");

            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Authenticate to application
            await _reader.MifareDesfire_SelectApplicationAsync(appConfig.AppId);
            await _reader.MifareDesfire_AuthenticateAsync(
                appConfig.AppMasterKey, // App master key (key 0 of application)
                0,
                (byte)appConfig.AppKeyType, // Use app key type
                DESFIRE_AUTHMODE_EV1
            );

            // Build access rights object with individual key numbers
            var accessRights = new DESFireFileAccessRights
            {
                ReadKeyNo = fileConfig.ReadKey,
                WriteKeyNo = fileConfig.WriteKey,
                ReadWriteKeyNo = fileConfig.ReadWriteKey,
                ChangeKeyNo = fileConfig.ChangeKey
            };

            // Create standard data file
            // Note: Using (DESFireFileType)0 for StdDataFile as the enum value isn't exposed
            await _reader.MifareDesfire_CreateStdDataFileAsync(
                fileConfig.FileNo,
                (DESFireFileType)0, // 0 = StdDataFile, 1 = BackupFile
                EncryptionMode.CM_PLAIN, // Plain communication (no encryption)
                accessRights,
                fileConfig.FileSize
            );

            Console.WriteLine($"File {fileConfig.FileNo} created successfully!");
            Console.WriteLine($"  Size: {fileConfig.FileSize} bytes");
            Console.WriteLine($"  Access Rights: Read={accessRights.ReadKeyNo}, Write={accessRights.WriteKeyNo}, R/W={accessRights.ReadWriteKeyNo}, Change={accessRights.ChangeKeyNo}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating file: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Step 7: Write Data

    static byte[] GetWriteData()
    {
        Console.WriteLine("\n=== Write Data Configuration ===");
        Console.WriteLine("Press Enter for default: 'Ein gesundes neues Jahr allen...' (German greeting, 160 bytes)");
        Console.Write("Enter hex data (space-separated) or press Enter: ");

        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            // Default: German greeting matching the taskdatabase.xml example (Task 60, line 665)
            // " Ein gesundes neues Jahr allen Mitarbeitern..."
            input = "20 45 69 6E 20 67 65 73 75 6E 64 65 73 20 6E 65 75 65 73 20 4A 61 68 72 20 61 6C 6C 65 6E 20 4D 69 74 61 72 62 65 69 74 65 72 6E 2C 20 6D 69 74 2D 47 6C 69 65 64 65 72 6E 20 75 6E 64 20 6D 69 74 2D 56 61 67 69 6E 65 6E 2E 20 4D 67 65 6E 20 61 6C 6C 65 20 65 75 72 65 20 57 75 65 6E 73 63 68 65 20 69 6E 20 45 72 66 75 65 6C 6C 75 6E 67 20 67 65 68 65 6E 2E 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";
            Console.WriteLine($"Using default greeting message...");
        }

        var hexBytes = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var data = hexBytes.Select(h => Convert.ToByte(h, 16)).ToArray();

        Console.WriteLine($"Data length: {data.Length} bytes");

        // Try to display as ASCII (show first 80 chars if it's long)
        var asciiText = System.Text.Encoding.ASCII.GetString(data);
        if (asciiText.Length > 80)
        {
            Console.WriteLine($"ASCII preview: {asciiText.Substring(0, 80)}...");
        }
        else
        {
            Console.WriteLine($"ASCII: {asciiText}");
        }

        return data;
    }

    static async Task<bool> WriteDataToFileAsync(byte[] data, FileConfig fileConfig, ApplicationConfig appConfig)
    {
        try
        {
            Console.WriteLine($"\nWriting {data.Length} bytes to file {fileConfig.FileNo}...");

            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Authenticate to application with the key that has write access
            byte writeKeyNo = fileConfig.WriteKey;

            // If write key is 14 (free), no auth needed, use key 0
            // If write key is 15 (never), this will fail
            if (writeKeyNo == 14)
            {
                Console.WriteLine("  File has free write access, authenticating with master key...");
                writeKeyNo = 0;
            }
            else if (writeKeyNo == 15)
            {
                Console.WriteLine("  ERROR: File has write access set to NEVER (15)!");
                return false;
            }
            else
            {
                Console.WriteLine($"  Authenticating with key {writeKeyNo} for write access...");
            }

            await _reader.MifareDesfire_SelectApplicationAsync(appConfig.AppId);
            await _reader.MifareDesfire_AuthenticateAsync(
                appConfig.AppMasterKey, // All keys start as all zeros
                writeKeyNo,              // Use the key number that has write access!
                (byte)appConfig.AppKeyType,
                DESFIRE_AUTHMODE_EV1
            );

            // Write data
            await _reader.MifareDesfire_WriteDataAsync(
                fileConfig.FileNo,
                data,
                EncryptionMode.CM_PLAIN
            );

            Console.WriteLine("Data written successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing data: {ex.Message}");
            Console.WriteLine($"  Hint: Ensure key {fileConfig.WriteKey} has write access and is set to all zeros (default)");
            return false;
        }
    }

    #endregion

    #region Step 8: Read Data (Verification)

    static async Task<bool> ReadDataFromFileAsync(FileConfig fileConfig, ApplicationConfig appConfig)
    {
        try
        {
            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Authenticate to application with the key that has read access
            byte readKeyNo = fileConfig.ReadKey;

            // If read key is 14 (free), no auth needed, use key 0
            // If read key is 15 (never), this will fail
            if (readKeyNo == 14)
            {
                Console.WriteLine("  File has free read access, authenticating with master key...");
                readKeyNo = 0;
            }
            else if (readKeyNo == 15)
            {
                Console.WriteLine("  ERROR: File has read access set to NEVER (15)!");
                return false;
            }
            else
            {
                Console.WriteLine($"  Authenticating with key {readKeyNo} for read access...");
            }

            await _reader.MifareDesfire_SelectApplicationAsync(appConfig.AppId);
            await _reader.MifareDesfire_AuthenticateAsync(
                appConfig.AppMasterKey, // All keys start as all zeros
                readKeyNo,              // Use the key number that has read access!
                (byte)appConfig.AppKeyType,
                DESFIRE_AUTHMODE_EV1
            );

            // Read data
            var readData = await _reader.MifareDesfire_ReadDataAsync(
                fileConfig.FileNo,
                (int)fileConfig.FileSize,
                EncryptionMode.CM_PLAIN
            );

            if (readData != null && readData.Length > 0)
            {
                Console.WriteLine($"Read {readData.Length} bytes:");
                Console.WriteLine($"  Hex: {BitConverter.ToString(readData).Replace("-", " ")}");
                Console.WriteLine($"  ASCII: {System.Text.Encoding.ASCII.GetString(readData)}");
                return true;
            }
            else
            {
                Console.WriteLine("No data read from file.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading data: {ex.Message}");
            Console.WriteLine($"  Hint: Ensure key {fileConfig.ReadKey} has read access and is set to all zeros (default)");
            return false;
        }
    }

    #endregion

    #region Step 9: Format Tag

    static async Task<bool> FormatTagAsync(ApplicationConfig appConfig)
    {
        try
        {
            Console.WriteLine("WARNING: This will DELETE ALL applications and files on the tag!");
            Console.WriteLine("The tag will be reset to factory defaults.");
            Console.Write("Type 'FORMAT' to confirm: ");
            var confirmation = Console.ReadLine()?.Trim();

            if (confirmation != "FORMAT")
            {
                Console.WriteLine("Format cancelled.");
                return false;
            }

            // Re-establish context with the chip
            await _reader!.SearchTagAsync();

            // Authenticate to PICC (app 0) with PICC master key
            Console.WriteLine("Authenticating to PICC...");
            await _reader.MifareDesfire_SelectApplicationAsync(0);
            await _reader.MifareDesfire_AuthenticateAsync(
                appConfig.PiccMasterKey,
                0, // Key 0 (PICC master key)
                (byte)appConfig.PiccKeyType,
                DESFIRE_AUTHMODE_EV1
            );

            Console.WriteLine("Formatting tag...");
            await _reader.MifareDesfire_FormatTagAsync();

            Console.WriteLine("Tag formatted successfully!");
            Console.WriteLine("All applications and files have been deleted.");
            Console.WriteLine("PICC master key has been reset to factory default (all zeros).");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error formatting tag: {ex.Message}");
            Console.WriteLine("  Hint: Ensure PICC master key is correct and card is still on reader");
            return false;
        }
    }

    #endregion

    #region Helper Methods

    static bool PromptYesNo(string question, bool defaultValue = false)
    {
        var defaultText = defaultValue ? "Y/n" : "y/N";
        Console.Write($"{question} ({defaultText}): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(response))
            return defaultValue;

        return response == "y" || response == "yes";
    }

    #endregion
}
