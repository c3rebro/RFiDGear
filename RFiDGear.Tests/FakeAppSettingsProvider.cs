using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using System.Threading.Tasks;

namespace RFiDGear.Tests
{
    /// <summary>A reader that answers the application settings it is told to, and counts how often it was asked.</summary>
    internal class FakeAppSettingsProvider : ElatecNetProvider
    {
        /// <summary>Gets or sets the key-settings byte the card reports.</summary>
        public byte KeySettings { get; set; }

        /// <summary>Gets or sets the maximum number of keys the card reports.</summary>
        public byte MaxNumberOfKeys { get; set; } = 1;

        /// <summary>Gets or sets the result of reading the settings.</summary>
        public ERROR Result { get; set; } = ERROR.NoError;

        /// <summary>Gets or sets whether reading the settings throws, as a reader that fails in a way nobody expected.</summary>
        public bool Throw { get; set; }

        /// <summary>Gets how often the settings were read.</summary>
        public int AppSettingsCalls { get; private set; }

        /// <summary>Gets or sets the result of authenticating to an application.</summary>
        public ERROR AuthenticationResult { get; set; } = ERROR.NoError;

        /// <summary>Gets how often an authentication was asked for.</summary>
        public int AuthCalls { get; private set; }

        /// <summary>Gets the key number the last authentication was asked for.</summary>
        public int LastAuthKeyNumber { get; private set; } = -1;

        /// <summary>Gets the key the last authentication was asked for.</summary>
        public string? LastAuthKey { get; private set; }

        /// <summary>Gets the key type the last authentication was asked for.</summary>
        public DESFireKeyType? LastAuthKeyType { get; private set; }

        /// <summary>Gets the application the last authentication was asked for.</summary>
        public int LastAuthAppId { get; private set; } = -1;

        /// <summary>
        /// Answers that a chip was read without reading one. The task execution service reads the chip before any task of
        /// a run, and the real reader would look for hardware.
        /// </summary>
        /// <returns>Success, with no chip.</returns>
        public override Task<ERROR> ReadChipPublic() => Task.FromResult(ERROR.NoError);

        /// <inheritdoc />
        public override Task<OperationResult> GetMifareDesfireAppSettings(
            string _applicationMasterKey,
            DESFireKeyType _keyType,
            int _keyNumberCurrent,
            int _appID,
            bool authenticateBeforeReading = true)
        {
            _ = _applicationMasterKey;
            _ = _keyType;
            _ = _keyNumberCurrent;
            _ = _appID;
            _ = authenticateBeforeReading;
            AppSettingsCalls++;
            if (Throw)
            {
                throw new System.InvalidOperationException("The reader failed.");
            }

            DesfireAppKeySetting = (DESFireKeySettings)KeySettings;
            MaxNumberOfAppKeys = MaxNumberOfKeys;

            return Task.FromResult(OperationResult.Success(Result));
        }

        /// <inheritdoc />
        public override Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            AuthCalls++;
            LastAuthKey = _applicationMasterKey;
            LastAuthKeyType = _keyType;
            LastAuthKeyNumber = _keyNumber;
            LastAuthAppId = _appID;

            return Task.FromResult(AuthenticationResult);
        }
    }
}
