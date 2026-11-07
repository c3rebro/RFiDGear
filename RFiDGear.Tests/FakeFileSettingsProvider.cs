using RFiDGear.Infrastructure;
using System.Threading.Tasks;

namespace RFiDGear.Tests
{
    /// <summary>A reader that serves the file settings it is told to, and counts how often it was asked.</summary>
    internal sealed class FakeFileSettingsProvider : FakeAppSettingsProvider
    {
        /// <summary>Gets or sets the settings a successful read returns.</summary>
        public DESFireFileSettings? FileSettings { get; set; }

        /// <summary>Gets or sets the result of reading the settings.</summary>
        public ERROR FileSettingsResult { get; set; } = ERROR.NoError;

        /// <summary>
        /// Gets or sets whether a read answers success without storing settings: a provider that fails without saying
        /// so, and keeps what it read before.
        /// </summary>
        public bool LeaveSettingsUnset { get; set; }

        /// <summary>Gets how often the settings were read.</summary>
        public int GetFileSettingsCalls { get; private set; }

        /// <summary>Gets the application the last read was for.</summary>
        public int LastFileSettingsAppId { get; private set; } = -1;

        /// <summary>Gets the file the last read was for.</summary>
        public int LastFileSettingsFileNo { get; private set; } = -1;

        /// <inheritdoc />
        public override Task<ERROR> GetMifareDesfireFileSettings(
            string _applicationMasterKey,
            DESFireKeyType _keyType,
            int _keyNumberCurrent = 0,
            int _appID = 0,
            int _fileNo = 0)
        {
            _ = _applicationMasterKey;
            _ = _keyType;
            _ = _keyNumberCurrent;
            GetFileSettingsCalls++;
            LastFileSettingsAppId = _appID;
            LastFileSettingsFileNo = _fileNo;

            if (FileSettingsResult != ERROR.NoError)
            {
                DesfireFileSettings = null;
                return Task.FromResult(FileSettingsResult);
            }

            if (!LeaveSettingsUnset)
            {
                DesfireFileSettings = FileSettings;
            }

            return Task.FromResult(ERROR.NoError);
        }
    }
}
