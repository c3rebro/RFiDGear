using System;

namespace Elatec.NET
{
    /// <summary>
    /// Type of a chip, as returned by the reader.
    /// </summary>
    public enum ChipType
    {
        NOTAG = 0,
        // LF Tags
        EM4102 = 0x40,    // "EM4x02/CASI-RUSCO" (aka IDRO_A)
        HITAG1S = 0x41,   // "HITAG 1/HITAG S"   (aka IDRW_B)
        HITAG2 = 0x42,    // "HITAG 2"           (aka IDRW_C)
        EM4150 = 0x43,    // "EM4x50"            (aka IDRW_D)
        AT5555 = 0x44,    // "T55x7"             (aka IDRW_E)
        ISOFDX = 0x45,    // "ISO FDX-B"         (aka IDRO_G)
        EM4026 = 0x46,    // N/A                 (aka IDRO_H)
        HITAGU = 0x47,    // N/A                 (aka IDRW_I)
        EM4305 = 0x48,    // "EM4305"            (aka IDRW_K)
        HIDPROX = 0x49,	// "HID Prox"
        TIRIS = 0x4A,	    // "ISO HDX/TIRIS"
        COTAG = 0x4B,	    // "Cotag"
        IOPROX = 0x4C,	// "ioProx"
        INDITAG = 0x4D,	// "Indala"
        HONEYTAG = 0x4E,	// "NexWatch"
        AWID = 0x4F,	    // "AWID"
        GPROX = 0x50,	    // "G-Prox"
        PYRAMID = 0x51,	// "Pyramid"
        KERI = 0x52,	    // "Keri"
        DEISTER = 0x53,	// "Deister"
        CARDAX = 0x54,	// "Cardax"
        NEDAP = 0x55,	    // "Nedap"
        PAC = 0x56,	    // "PAC"
        IDTECK = 0x57,	// "IDTECK"
        ULTRAPROX = 0x58,	// "UltraProx"
        ICT = 0x59,	    // "ICT"
        ISONAS = 0x5A,	// "Isonas"
        // HF Tags
        MIFARE = 0x80,	// "ISO14443A/MIFARE"
        ISO14443B = 0x81,	// "ISO14443B"
        ISO15693 = 0x82,	// "ISO15693"
        LEGIC = 0x83,	    // "LEGIC"
        HIDICLASS = 0x84,	// "HID iCLASS"
        FELICA = 0x85,	// "FeliCa"
        SRX = 0x86,	    // "SRX"
        NFCP2P = 0x87,	// "NFC Peer-to-Peer"
        BLE = 0x88,	    // "Bluetooth Low Energy"
        TOPAZ = 0x89,     // "Topaz"
        CTS = 0x8A,       // "CTS256 / CTS512"
        BLELC = 0x8B,     // "Bluetooth Low Energy LEGIC Connect"

    }

    /// <summary>
    /// Type of Mifare from NXP AN10833
    /// </summary>
    /// <remarks>
    /// <code>
    /// Mifare Classic =    0x10 - 0x1F
    /// NXP SAM =           0x20 - 0x2F
    /// Mifare Plus =       0x30 - 0x3F
    /// Mifare Desfire =    0x40 - 0x7F
    /// Desfire Light =     0x80 - 0x8F
    /// Mifare Ultralight = 0x90 - 0x9F
    /// Mifare Mini =       0xA0 - 0xAF
    /// NXP NTAG =          0xB0 - 0xBF
    /// NXP ICODE =         0xC0 - 0xCF
    ///
    /// 0bxxxx 0000 = nxp type
    /// 0b0000 xxxx = nxp subtype
    /// 0bxxxx 1xxx = smartmx variant
    /// </code>
    /// </remarks>
    [Flags]
    public enum MifareChipSubType
    {
        /* CUSTOM
         * 
         * Mifare Classic = 0x10 - 0x1F
         * NXP SAM = 0x20 - 0x2F
         * Mifare Plus = 0x30 - 0x3F
         * Mifare Desfire = 0x40 - 0x7F
         * Mifare Desfire Light = 0x80 - 0x8F
         * Mifare Ultralight = 0x90 - 0x9F
         * Mifare Mini = 0xA0 - 0xAF
         * NXP NTAG = 0xB0 - 0xBF
         * NXP ICODE = 0xC0 - 0xCF
         * 
         * 0bxxxx 0000 = mifare type
         * 0x0000 xxxx = mifare subtype
         */

        /* 
         * 0b0001 xxxx = mifare classic
         * 0b0001 1xxx = smartmx classic 
         *          
         * 0b0001 0000 = mifare classic - 1k
         * 0b0001 0001 = mifare classic - 2k
         * 0b0001 0010 = mifare classic - 4k
         * 
         * 0b0001 1000 = smartmx classic - 1k
         * 0b0001 1001 = smartmx classic - 2k
         * 0b0001 1010 = smartmx classic - 4k
         */
        NOTAG = 0x0,
        Unspecified = 0x01,

        MifareClassic = 0x10,
        Mifare1K = 0x11,
        Mifare2K = 0x12,
        Mifare4K = 0x13,

        SmartMX_Mifare_1K = 0x19,
        SmartMX_Mifare_2K = 0x1A,
        SmartMX_Mifare_4K = 0x1B,

        /* 0b0010 0000 = MifareSAM
         * 
         * 0b0010 0001 = SAM_AV1
         * 0b0010 0010 = SAM_AV2
         */

        MifareSAM = 0x20,
        SAM_AV1 = 0x21,
        SAM_AV2 = 0x22,

        /* 0b0011 xxxx = Mifare Plus
         * 
         * 0b0011 00xx = Mifare Plus SL0
         * 0b0011 0000 = Mifare Plus SL0 - 1k
         * 0b0011 0001 = Mifare Plus SL0 - 2k
         * 0b0011 0010 = Mifare Plus SL0 - 4k
         * 
         * 0b0011 01xx = Mifare Plus SL1
         * 0b0011 0100 = Mifare Plus SL1 - 1k
         * 0b0011 0101 = Mifare Plus SL1 - 2k
         * 0b0011 0110 = Mifare Plus SL1 - 4k
         * 
         * 0b0011 10xx = Mifare Plus SL2
         * 0b0011 1000 = Mifare Plus SL2 - 1k
         * 0b0011 1001 = Mifare Plus SL2 - 2k
         * 0b0011 1010 = Mifare Plus SL2 - 4k
         * 
         * 0b0011 11xx = Mifare Plus SL3
         * 0b0011 1100 = Mifare Plus SL3 - 1k
         * 0b0011 1101 = Mifare Plus SL3 - 2k
         * 0b0011 1110 = Mifare Plus SL3 - 4k
        */

        MifarePlus = 0x30,
        MifarePlus_SL0_1K = 0x31,
        MifarePlus_SL0_2K = 0x32,
        MifarePlus_SL0_4K = 0x33,

        MifarePlus_SL1_1K = 0x34,
        MifarePlus_SL1_2K = 0x35,
        MifarePlus_SL1_4K = 0x36,

        MifarePlus_SL2_1K = 0x38,
        MifarePlus_SL2_2K = 0x39,
        MifarePlus_SL2_4K = 0x3A,

        MifarePlus_SL3_1K = 0x3C,
        MifarePlus_SL3_2K = 0x3D,
        MifarePlus_SL3_4K = 0x3E,

        /* 0b01xx xxxx = Mifare Desfire
         * 0b01xx 1xxx = SmartMX Desfire
         * 0b0100 xxxx = EV0
         * 0b0101 xxxx = EV1
         * 0b0110 xxxx = EV2
         * 0b0111 xxxx = EV3
         * 
         * 0b0100 0000 = Mifare Desfire EV0 - 256
         * 0b0100 0001 = Mifare Desfire EV0 - 1k
         * 0b0100 0010 = Mifare Desfire EV0 - 2k
         * 0b0100 0011 = Mifare Desfire EV0 - 4k
         * 
         * 0b0100 1xxx = SmartMX Desfire EV0
         * 0b0100 1000 = SmartMX Desfire EV0 - 256
         * 0b0100 1001 = SmartMX Desfire EV0 - 1k
         * 0b0100 1010 = SmartMX Desfire EV0 - 2k
         * 0b0100 1011 = SmartMX Desfire EV0 - 4k
         * 
         * 0b0101 0xxx = Mifare Desfire EV1
         * 0b0101 0000 = Mifare Desfire EV1 - 256
         * 0b0101 0001 = Mifare Desfire EV1 - 2k
         * 0b0101 0010 = Mifare Desfire EV1 - 4k
         * 0b0101 0011 = Mifare Desfire EV1 - 8k
         * 
         * 0b0101 1xxx = SmartMX Desfire EV1
         * 0b0101 1000 = SmartMX Desfire EV1 - 256
         * 0b0101 1001 = SmartMX Desfire EV1 - 2k
         * 0b0101 1010 = SmartMX Desfire EV1 - 4k
         * 0b0101 1011 = SmartMX Desfire EV1 - 8k
         * 
         * 0b0110 0xxx = Mifare Desfire EV2
         * 0b0110 0000 = Mifare Desfire EV2 - 2k
         * 0b0110 0001 = Mifare Desfire EV2 - 4k
         * 0b0110 0010 = Mifare Desfire EV2 - 8k
         * 0b0110 0011 = Mifare Desfire EV2 - 16k
         * 0b0110 0100 = Mifare Desfire EV2 - 32k
         * 
         * 0b0110 1xxx = SmartMX Desfire EV2
         * 0b0110 1000 = SmartMX Desfire EV2 - 2k
         * 0b0110 1001 = SmartMX Desfire EV2 - 4k
         * 0b0110 1010 = SmartMX Desfire EV2 - 8k
         * 0b0110 1011 = SmartMX Desfire EV2 - 16k
         * 0b0110 1100 = SmartMX Desfire EV2 - 32k
         * 
         * 0b0111 0xxx = Mifare Desfire EV3
         * 0b0111 0000 = Mifare Desfire EV3 - 2k
         * 0b0111 0001 = Mifare Desfire EV3 - 4k
         * 0b0111 0010 = Mifare Desfire EV3 - 8k
         * 0b0111 0011 = Mifare Desfire EV3 - 16k
         * 0b0111 0100 = Mifare Desfire EV3 - 32k
         * 
         * 0b0111 1xxx = SmartMX Desfire EV3
         * 0b0111 1000 = SmartMX Desfire EV3 - 2k
         * 0b0111 1001 = SmartMX Desfire EV3 - 4k
         * 0b0111 1010 = SmartMX Desfire EV3 - 8k
         * 0b0111 1011 = SmartMX Desfire EV3 - 16k
         * 0b0111 1100 = SmartMX Desfire EV3 - 32k
         * 
        */

        DESFire = 0x40,
        DESFireEV0 = 0x40,
        DESFireEV0_256 = 0x41,
        DESFireEV0_1K = 0x42,
        DESFireEV0_2K = 0x43,
        DESFireEV0_4K = 0x44,
        // 0x44 - 0x47 = RFU

        SmartMX_DESFire = 0x48,
        SmartMX_DESFire_Generic = 0x48,
        SmartMX_DESFireEV0_256 = 0x49,
        SmartMX_DESFireEV0_1K = 0x4A,
        SmartMX_DESFireEV0_2K = 0x4B,
        SmartMX_DESFireEV0_4K = 0x4C,
        // 0x4C - 0x4F = RFU

        DESFireEV1 = 0x50,
        DESFireEV1_256 = 0x51,
        DESFireEV1_2K = 0x52,
        DESFireEV1_4K = 0x53,
        DESFireEV1_8K = 0x54,
        // 0x55 - 0x57 = RFU

        SmartMX_DESFireEV1_256 = 0x59,
        SmartMX_DESFireEV1_2K = 0x5A,
        SmartMX_DESFireEV1_4K = 0x5B,
        SmartMX_DESFireEV1_8K = 0x5C,
        // 0x5C - 0x5F = RFU

        DESFireEV2 = 0x60,
        DESFireEV2_2K = 0x61,
        DESFireEV2_4K = 0x62,
        DESFireEV2_8K = 0x63,
        DESFireEV2_16K = 0x64,
        DESFireEV2_32K = 0x65,
        // 0x5C - 0x5F = RFU

        SmartMX_DESFireEV2_2K = 0x69,
        SmartMX_DESFireEV2_4K = 0x6A,
        SmartMX_DESFireEV2_8K = 0x6B,
        SmartMX_DESFireEV2_16K = 0x6C,
        SmartMX_DESFireEV2_32K = 0x6D,
        // 0x5C - 0x5F = RFU

        DESFireEV3 = 0x70,
        DESFireEV3_2K = 0x71,
        DESFireEV3_4K = 0x72,
        DESFireEV3_8K = 0x73,
        DESFireEV3_16K = 0x74,
        DESFireEV3_32K = 0x75,
        // 0x5C - 0x5F = RFU

        SmartMX_DESFireEV3_2K = 0x79,
        SmartMX_DESFireEV3_4K = 0x7A,
        SmartMX_DESFireEV3_8K = 0x7B,
        SmartMX_DESFireEV3_16K = 0x7C,
        SmartMX_DESFireEV3_32K = 0x7D,
        // 0x5C - 0x5F = RFU

        DESFireLight = 0x8000,

        MifareUltralight = 0x90,
        MifareUltralightC = 0x91,
        MifareUltralightC_EV1 = 0x92,

        NTAG_210 = 0xA0,
        NTAG_211 = 0xA1,
        NTAG_212 = 0xA2,
        NTAG_213 = 0xA3,
        NTAG_214 = 0xA4,
        NTAG_215 = 0xA5,
        NTAG_216 = 0xA6,
        // 0xA7 - 0xA9 = RFU
        NTAG_424 = 0xAA,
        NTAG_426 = 0xAB,

        MifareMini = 0xB0
    }

    [Flags]
    public enum LFTagTypes : uint
    {
        NOTAG = 0,
        // LF Tags
        EM4102 = 1 << 0,    // "EM4x02/CASI-RUSCO" (aka IDRO_A)
        HITAG1S = 1 << 1,   // "HITAG 1/HITAG S"   (aka IDRW_B)
        HITAG2 = 1 << 2,    // "HITAG 2"           (aka IDRW_C)
        EM4150 = 1 << 3,    // "EM4x50"            (aka IDRW_D)
        AT5555 = 1 << 4,    // "T55x7"             (aka IDRW_E)
        ISOFDX = 1 << 5,    // "ISO FDX-B"         (aka IDRO_G)
        EM4026 = 1 << 6,    // N/A                 (aka IDRO_H)
        HITAGU = 1 << 7,    // N/A                 (aka IDRW_I)
        EM4305 = 1 << 8,    // "EM4305"            (aka IDRW_K)
        HIDPROX = 1 << 9,	// "HID Prox"
        TIRIS = 1 << 0xA,	    // "ISO HDX/TIRIS"
        COTAG = 1 << 0xB,	    // "Cotag"
        IOPROX = 1 << 0xC,	// "ioProx"
        INDITAG = 1 << 0xD,	// "Indala"
        HONEYTAG = 1 << 0xE,	// "NexWatch"
        AWID = 1 << 0xF,	    // "AWID"
        GPROX = 1 << 0x10,	    // "G-Prox"
        PYRAMID = 1 << 0x11,	// "Pyramid"
        KERI = 1 << 0x12,	    // "Keri"
        DEISTER = 1 << 0x13,	// "Deister"
        CARDAX = 1 << 0x14,	// "Cardax"
        NEDAP = 1 << 0x15,	    // "Nedap"
        PAC = 1 << 0x16,	    // "PAC"
        IDTECK = 1 << 0x17,	// "IDTECK"
        ULTRAPROX = 1 << 0x18,	// "UltraProx"
        ICT = 1 << 0x19,	    // "ICT"
        ISONAS = 1 << 0x1A,	// "Isonas"

        AllLFTags = 0xFFFFFFFF,
    }

    [Flags]
    public enum HFTagTypes : uint
    {
        NOTAG = 0,
        // HF Tags
        MIFARE = 1 << 0,	// "ISO14443A/MIFARE"
        ISO14443B = 1 << 1,	// "ISO14443B"
        ISO15693 = 1 << 2,	// "ISO15693"
        LEGIC = 1 << 3,	    // "LEGIC"
        HIDICLASS = 1 << 4,	// "HID iCLASS"
        FELICA = 1 << 5,	// "FeliCa"
        SRX = 1 << 6,	    // "SRX"
        NFCP2P = 1 << 7,	// "NFC Peer-to-Peer"
        BLE = 1 << 8,	    // "Bluetooth Low Energy"
        TOPAZ = 1 << 9,     // "Topaz"
        CTS = 1 << 0xA,       // "CTS256 / CTS512"
        BLELC = 1 << 0xB,     // "Bluetooth Low Energy LEGIC Connect"

        AllHFTags = 0xFFFFFFFF,
    }

    /// <summary>
    /// A response to a TWN Simple Protocol command always starts with a byte, which reflects execution of the command on protocol level.
    /// </summary>
    public enum ResponseError : byte
    {
        None = 0,
        UnknownFunction = 1,
        MissingParameter = 2,
        UnusedParameters = 3,
        InvalidFunction = 4,
        ParserError = 5,
    }

    /// <summary>
    ///     Values returned by <see cref="TWN4ReaderDevice.GetLastErrorAsync"/>, at least theoretically.
    ///     In practice, the method also returns undocumented error codes.
    /// </summary>
    public enum ReaderError : uint
    {
        None = 0,

        // --- General Errors ---
        OutOfMemory = 1,
        IsAlreadyInit = 2,
        NotInit = 3,
        IsAlreadyOpen = 4,
        NotOpen = 5,
        Range = 6,
        Parameter = 7,
        General = 8,
        NotSupported = 9,
        State = 10,
        Compatibility = 11,
        Data = 12,

        // --- Storage Errors ---
        UnknownStorageID = 100,
        WrongIndex = 101,
        FlashErase = 102,
        FlashWrite = 103,
        SectorNotFound = 104,
        StorageFull = 105,
        StorageInvalid = 106,
        TransactionLimit = 107,

        // --- File Errors ---
        UnknownFS = 200,
        FileNotFound = 201,
        FileAlreadyExists = 202,
        EndOfFile = 203,
        StorageNotFound = 204,
        StorageAlreadyMounted = 205,
        AccessDenied = 206,
        FileCorrupt = 207,
        InvalidFileEnv = 208,
        InvalidFileID = 209,
        ResourceLimit = 210,

        // --- I2C Errors ---
        Timeout = 300,
        PecErr = 301,
        Ovr = 302,
        /// <summary>
        /// Acknowledge Error
        /// </summary>
        AF = 303,
        Arlo = 304,
        /// <summary>
        /// Bus Error
        /// </summary>
        BErr = 305
    }

    #region GPIOs

    /// <summary>
    /// Bitmasks of GPIOs
    /// </summary>
    [Flags]
    public enum Gpios : byte
    {
        GPIO0 = 0x0001,
        GPIO1 = 0x0002,
        GPIO2 = 0x0004,
        GPIO3 = 0x0008,
        GPIO4 = 0x0010,
        GPIO5 = 0x0020,
        GPIO6 = 0x0040,
        GPIO7 = 0x0080,
    }

    /// <summary>
    /// GPIO Pullup/Pulldown
    /// </summary>
    public enum GpioPullType : byte
    {
        NoPull = 0,
        PullUp = 1,
        PullDown = 2
    }

    /// <summary>
    /// GPIO Output Type
    /// </summary>
    public enum GpioOutputType : byte
    {
        PushPull = 0,
        OpenDrain = 1
    }

    #endregion

    /// <summary>
    ///     Colored LEDs
    /// </summary>
    /// <remarks>
    ///     REDLED = GPIO0,
    ///     GREENLED = GPIO1,
    ///     YELLOWLED = GPIO2,
    ///     BLUELED = GPIO2.
    ///     Attention: Yellow and Blue have the same id!
    /// </remarks>
    [Flags]
    public enum Leds : byte
    {
        Red = Gpios.GPIO0,
        Green = Gpios.GPIO1,
        Yellow = Gpios.GPIO2,
        Blue = Gpios.GPIO2,
        All = Red | Green | Yellow | Blue
    }

    /// <summary>
    /// Pitch of Readerpiezo to be used with Beep() or Note() used by PlayMusic().
    /// </summary>
    public enum NotePitch
    {
        PAUSE = 0,

        C3 = 1047,
        CIS3 = 1109,
        DES3 = 1109,
        D3 = 1175,
        DIS3 = 1245,
        ES3 = 1245,
        E3 = 1319,
        F3 = 1397,
        FIS3 = 1480,
        GES3 = 1480,
        G3 = 1568,
        GIS3 = 1661,
        AES3 = 1661,
        A3 = 1760,
        B3 = 1865,
        H3 = 1976,
        C4 = 2093,
        CIS4 = 2217,
        DES4 = 2217,
        D4 = 2349,
        DIS4 = 2489,
        ES4 = 2489,
        E4 = 2637,
        F4 = 2794,
        FIS4 = 2960,
        GES4 = 2960,
        G4 = 3136,
        GIS4 = 3322,
        AES4 = 3322,
        A4 = 3520,
        AIS4 = 3729,
        B4 = 3729,
        H4 = 3951,
        C5 = 4186,
        CIS5 = 4435,
        DES5 = 4435,
        D5 = 4699,
        DIS5 = 4978,
        ES5 = 4978,
        E5 = 5274,
        F5 = 5588,

        LOW = 2057,
        HIGH = 2400
    }

    /// <summary>
    ///     TODO: Elatec references some constants in TWN4 API reference, but without their values:
    ///     USBTYPE_CCID_HID: CCID + HID (compound device),
    ///     USBTYPE_REPORTS: CCID + HID reports,
    ///     USBTYPE_CCID_CDC: CCID + CDC (compound device),
    ///     USBTYPE_CCID: CCID
    /// </summary>
    public enum UsbType : byte
    {
        None = 0,
        /// <summary>
        /// CDC device (virtual COM port)
        /// </summary>
        CDC = 1,
        Keyboard = 4,
    }

    public enum DeviceType : byte
    {
        LegicNfc = 10,
        MifareNfc = 11,
        Legic63 = 12,
    }
}