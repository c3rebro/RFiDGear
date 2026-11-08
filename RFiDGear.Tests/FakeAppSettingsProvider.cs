using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.AccessControl;
using RFiDGear.Infrastructure.ReaderProviders;
using System.Threading.Tasks;

namespace RFiDGear.Tests
{
    /// <summary>A reader that answers the application settings it is told to, and counts how often it was asked.</summary>
    internal sealed class FakeAppSettingsProvider : ElatecNetProvider
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

        /// <summary>Gets or sets what happens while the card answers a read of the settings, such as an edit of the dialog.</summary>
        public System.Action? DuringSettingsRead { get; set; }

        /// <summary>Gets or sets a task that an authentication waits for, as a card that takes its time.</summary>
        public Task? AuthenticationGate { get; set; }

        /// <summary>Gets or sets the result of authenticating to an application.</summary>
        public ERROR AuthenticationResult { get; set; } = ERROR.NoError;

        /// <summary>Gets how often an authentication was asked for.</summary>
        public int AuthCalls { get; private set; }

        /// <summary>Gets the key number the last authentication was asked for.</summary>
        public int LastAuthKeyNumber { get; private set; } = -1;

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
            DuringSettingsRead?.Invoke();
            if (Throw)
            {
                throw new System.InvalidOperationException("The reader failed.");
            }

            DesfireAppKeySetting = (DESFireKeySettings)KeySettings;
            MaxNumberOfAppKeys = MaxNumberOfKeys;

            return Task.FromResult(OperationResult.Success(Result));
        }

        /// <inheritdoc />
        public override async Task<ERROR> AuthToMifareDesfireApplication(string _applicationMasterKey, DESFireKeyType _keyType, int _keyNumber, int _appID)
        {
            _ = _applicationMasterKey;
            _ = _keyType;
            _ = _appID;
            AuthCalls++;
            LastAuthKeyNumber = _keyNumber;

            if (AuthenticationGate != null)
            {
                await AuthenticationGate;
            }

            return AuthenticationResult;
        }
    }
}
