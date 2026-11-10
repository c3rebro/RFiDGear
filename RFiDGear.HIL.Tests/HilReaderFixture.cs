using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.ReaderProviders;

using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Finds the reader for the tests of the reader scan. Unlike <see cref="HilFixture"/> it needs no DESFire card: an
    /// Elatec TWN4 is found by the reader itself, with or without a card on it. A PC/SC reader cannot be told from no
    /// reader while it has no card (the host says so, too), so it is found only with a card on it.
    /// Nothing is done unless the tests were asked for (see <see cref="HilSettings"/>): the fixture is also created
    /// when every test of its collection is skipped.
    /// </summary>
    public sealed class HilReaderFixture : IAsyncLifetime
    {
        /// <summary>Gets the kind of reader that was found, or <see cref="ReaderTypes.None"/>.</summary>
        public ReaderTypes Kind { get; private set; } = ReaderTypes.None;

        /// <summary>Gets why no reader was found, or <c>null</c> when one was.</summary>
        public string? FailureReason { get; private set; }

        /// <inheritdoc/>
        public async Task InitializeAsync()
        {
            if (!HilSettings.Current.HardwareRequested)
            {
                return;
            }

            HilFixture.ConfigureSerilog();

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                if (await IsReaderPresentAsync(ReaderTypes.Elatec, needsCard: false))
                {
                    Kind = ReaderTypes.Elatec;
                    return;
                }

                if (await IsReaderPresentAsync(ReaderTypes.PCSC, needsCard: true))
                {
                    Kind = ReaderTypes.PCSC;
                    return;
                }

                FailureReason = "No reader was found. Connect an Elatec TWN4 reader (it needs no card), or put a card on a PC/SC " +
                                "reader, close every other program and RFiDGear window that uses the reader, and run the tests again.";
            });
        }

        /// <inheritdoc/>
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Makes a host for one test, so that every test starts with a host that has not scanned yet. When tests were asked
        /// for but no reader was found, the test ends as the DESFire tests do: with the dynamic skip of the assert library,
        /// which xunit 2 reports as a failure with the message prefix <c>$XunitDynamicSkip$</c> and which
        /// <c>Run-HilTests.ps1</c> counts as skipped.
        /// </summary>
        /// <returns>The host; dispose it at the end of the test.</returns>
        public HeadlessExtensionHost NewHost()
        {
            if (Kind == ReaderTypes.None)
            {
                throw SkipException.ForSkip(FailureReason ?? "No reader was found.");
            }

            // The other fixture may have chosen another kind of reader since.
            ReaderDevice.Reader = Kind;

            return new HeadlessExtensionHost("hardware test");
        }

        private static async Task<bool> IsReaderPresentAsync(ReaderTypes readerType, bool needsCard)
        {
            try
            {
                ReaderDevice.Reader = readerType;

                var device = ReaderDevice.Instance;
                if (device == null)
                {
                    return false;
                }

                var error = await device.ReadChipPublic();

                // The Elatec provider answers NoError without a reader, too: only IsConnected says that one is attached.
                return needsCard ? error == ERROR.NoError : device.IsConnected;
            }
            catch (Exception ex)
            {
                _ = ex; // reader absent or SDK error: the next kind is tried
                return false;
            }
        }
    }

    /// <summary>The collection of the reader-scan tests; one at a time, because they share one reader.</summary>
    [CollectionDefinition("HILReader")]
    public sealed class HilReaderCollection : ICollectionFixture<HilReaderFixture> { }
}
