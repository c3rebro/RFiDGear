using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RFiDGear.Infrastructure;

namespace RFiDGear.Models
{
    /// <summary>
    /// Holds runtime-configurable defaults for <see cref="DefaultSpecification"/> instances.
    /// </summary>
    public class DefaultSpecificationRuntimeDefaults
    {
        private const string RuntimeDefaultsFileName = "runtime-defaults.json";

        static DefaultSpecificationRuntimeDefaults()
        {
            Current = LoadFromConfiguration();
        }

        public static DefaultSpecificationRuntimeDefaults Current { get; private set; }

        public static string RuntimeDefaultsFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RFiDGear",
            RuntimeDefaultsFileName);

        public string DefaultReaderName { get; set; } = string.Empty;

        public ReaderTypes DefaultReaderProvider { get; set; } = ReaderTypes.None;

        public string DefaultLanguage { get; set; } = "english";

        public bool DefaultAutoPerformTasksEnabled { get; set; }
            = false;

        public bool AutoCheckForUpdates { get; set; } = true;

        public bool AutoLoadProjectOnStart { get; set; }
            = false;

        public string LastUsedProjectPath { get; set; } = string.Empty;

        public string LastUsedComPort { get; set; } = "USB";

        public string LastUsedBaudRate { get; set; } = "9600";

        public List<MifareClassicDefaultKeys> MifareClassicDefaultSecuritySettings { get; set; }
            = new List<MifareClassicDefaultKeys>
            {
                new MifareClassicDefaultKeys(0, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(1, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(2, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(3, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(4, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(5, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(6, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(7, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(8, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(9, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(10, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(11, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(12, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(13, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(14, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF"),
                new MifareClassicDefaultKeys(15, "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF")
            };

        public List<MifareDesfireDefaultKeys> MifareDesfireDefaultSecuritySettings { get; set; }
            = new List<MifareDesfireDefaultKeys>
            {
                new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardApplicationMasterKey, DESFireKeyType.DF_KEY_AES, "00000000000000000000000000000000"),
                new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardCardMasterKey, DESFireKeyType.DF_KEY_DES, "00000000000000000000000000000000"),
                new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardReadKey, DESFireKeyType.DF_KEY_AES, "00000000000000000000000000000000"),
                new MifareDesfireDefaultKeys(KeyType_MifareDesFireKeyType.DefaultDesfireCardWriteKey, DESFireKeyType.DF_KEY_AES, "00000000000000000000000000000000")
            };

        public string ClassicCardDefaultSectorTrailer { get; set; }
            = "FFFFFFFFFFFF,FF0780C3,FFFFFFFFFFFF";

        public List<string> ClassicCardDefaultQuickCheckKeys { get; set; }
            = new List<string>
            {
                "FFFFFFFFFFFF","A1B2C3D4E5F6","1A2B3C4D5E6F",
                "000000000000","C75680590F31","010203040506",
                "A0B0C0D0E0F0","A1B1C1D1E1F1","987654321ABC",
                "A0A1A2A3A4A5","B0B1B2B3B4B5","4D3A99C351DD",
                "1A982C7E459A","D3F7D3F7D3F7","AABBCCDDEEFF",
                "0CBFD39CE01E"
            };

        private static DefaultSpecificationRuntimeDefaults LoadFromConfiguration()
        {
            EnsureDirectoryExists(Path.GetDirectoryName(RuntimeDefaultsFilePath));

            var defaults = new DefaultSpecificationRuntimeDefaults();

            if (!File.Exists(RuntimeDefaultsFilePath))
            {
                WriteTemplateFile(RuntimeDefaultsFilePath, defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(RuntimeDefaultsFilePath);
                JsonConvert.PopulateObject(json, defaults);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
            }

            return defaults;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void WriteTemplateFile(string path, DefaultSpecificationRuntimeDefaults defaults)
        {
            var json = JsonConvert.SerializeObject(defaults, Formatting.Indented);

            File.WriteAllText(path, json);
        }
    }
}
