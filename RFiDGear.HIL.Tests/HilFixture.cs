using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;

using Serilog;

using System;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// xUnit collection fixture that connects to a reader and verifies a DESFire card is present.
    /// All tests that use this fixture are skipped when no reader or card is found.
    /// </summary>
    public sealed class HilFixture : IAsyncLifetime
    {
        /// <summary>
        /// Not null when a DESFire card was detected successfully.
        /// Tests must call <see cref="SkipIfNoHardware"/> at the top of each test method.
        /// </summary>
        public RFiDGear.Infrastructure.ExtensionHost.HeadlessExtensionHost? Host { get; private set; }

        /// <summary>
        /// Non-null skip message when no hardware is present.
        /// </summary>
        public string? SkipReason { get; private set; }

        /// <summary>
        /// Skips the current test if no reader/card hardware was detected during fixture setup.
        /// </summary>
        public void SkipIfNoHardware()
        {
            if (SkipReason != null)
                throw Xunit.Sdk.SkipException.ForSkip(SkipReason);
        }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            ConfigureSerilog();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                // Try Elatec TWN4 first (auto-discovers USB readers).
                if (await TryConnectReaderAsync(ReaderTypes.Elatec))
                    return;

                // Fall back to PC/SC.
                if (await TryConnectReaderAsync(ReaderTypes.PCSC))
                    return;

                SkipReason = "No reader detected. Connect an Elatec TWN4 or PC/SC reader with a DESFire card.";
            });
        }

        /// <inheritdoc/>
        public async Task DisposeAsync()
        {
            if (Host != null)
            {
                await StaTestRunner.RunOnStaThreadAsync(() => CleanupTestAidAsync());
                Host.Dispose();
            }
        }

        /// <summary>
        /// Deletes the reserved HIL test AID from the card if it exists.
        /// Called both after a full test run and at the start of each test (guard).
        /// </summary>
        internal async Task CleanupTestAidAsync()
        {
            if (Host == null) return;

            try
            {
                // Always use PICC master key for cleanup: the app key type may vary across tests,
                // but the factory PICC master key (DES, all-zero) is never changed by the tests.
                var tasks = HilTaskBuilder.BuildDeleteApplication(
                    HilConstants.TestAppId,
                    DESFireKeyType.DF_KEY_DES,
                    HilConstants.DefaultKeyDes,
                    DesfireDeleteAuthMethod.PiccMasterKey);

                Host.Project.ReplaceTaskCollection(tasks);
                // Ignore the result — the app may not exist.
                await Host.Execution.ExecuteAllAsync();
                Host.Execution.ResetStatus();
            }
            catch
            {
                // Intentionally silent — cleanup is best-effort.
            }
        }

        // ── private helpers ──────────────────────────────────────────────────

        private static void ConfigureSerilog()
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RFiDGear", "log");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDir, "hil-log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private async Task<bool> TryConnectReaderAsync(ReaderTypes readerType)
        {
            try
            {
                ReaderDevice.Reader = readerType;

                var device = ReaderDevice.Instance;
                if (device == null) return false;

                var error = await device.ReadChipPublic();
                if (error != ERROR.NoError) return false;

                var cardType = device.GenericChip?.CardType ?? CARD_TYPE.Unspecified;
                if (!IsDesFire(cardType))
                {
                    SkipReason = $"Card present but not DESFire (detected: {cardType}). " +
                                 "Place a DESFire EV1/EV2/EV3 card on the reader.";
                    return false;
                }

                Host = new RFiDGear.Infrastructure.ExtensionHost.HeadlessExtensionHost();
                return true;
            }
            catch (Exception ex)
            {
                _ = ex; // reader absent or SDK error — try next type
                return false;
            }
        }

        private static bool IsDesFire(CARD_TYPE cardType)
        {
            var value = (int)cardType;
            // DESFire range: 0x4000–0x7FFF  (EV0, EV1, EV2, EV3, SmartMX variants)
            return value >= 0x4000 && value <= 0x7FFF;
        }
    }

    [CollectionDefinition("HIL")]
    public sealed class HilCollection : ICollectionFixture<HilFixture> { }
}
