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

        /// <summary>Gets how often the settings were read.</summary>
        public int AppSettingsCalls { get; private set; }

        /// <inheritdoc />
        public override Task<OperationResult> GetMifareDesfireAppSettings(
            string _applicationMasterKey,
            DESFireKeyType _keyType,
            int _keyNumberCurrent,
            int _appID,
            bool authenticateBeforeReading = true)
        {
            AppSettingsCalls++;
            DesfireAppKeySetting = (DESFireKeySettings)KeySettings;
            MaxNumberOfAppKeys = MaxNumberOfKeys;

            return Task.FromResult(OperationResult.Success(Result));
        }
    }
}
