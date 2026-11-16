#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Classic;
using RFiDGear.Infrastructure.ReaderProviders;

using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The MIFARE Classic read, write and MAD calls of the reader against a real reader and a real card as it left the factory (see
    /// <c>CLASSIC.md</c>). The steps are in <see cref="ClassicScenarios"/>, which the unit tests also run against a chip in memory; this class
    /// only supplies the reader and the card.
    /// <para>
    /// They need the reader-scan tests (<c>readerScan</c> in <c>hil.settings.json</c>). The three that write to the card also need
    /// <c>cardWrites</c>. Which reader is used is <c>readerKind</c>. A card that is not as it left the factory makes a case skip.
    /// </para>
    /// </summary>
    [Collection("HILClassic")]
    public sealed class ClassicWorkflowTests
    {
        private readonly HilReaderFixture _fixture;

        /// <summary>Creates the cases with the reader that was found.</summary>
        /// <param name="fixture">The reader.</param>
        public ClassicWorkflowTests(HilReaderFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>Sector 1 is read with the factory key (and sector 33 of a 4K, which has 16 blocks; a smaller chip refuses a sector it does not have).</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AFactorySector_IsRead_WithTheFactoryKey() => Run((device, size, _) => ClassicScenarios.AFactorySectorIsReadAsync(device, size));

        /// <summary>A wrong key is refused (nothing is written), and the next read with the right key works.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AWrongKey_IsRefused_AndTheNextReadWorks() => Run((device, _, _) => ClassicScenarios.AWrongKeyIsRefused_AndTheNextReadWorksAsync(device));

        /// <summary>Writes block 5 with other bytes, reads it back and puts the old bytes back.</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task ABlock_IsWritten_ReadBack_AndPutBack() => Run((device, _, who) => ClassicScenarios.ABlockIsWrittenReadBackAndPutBackAsync(device, who));

        /// <summary>Writes the data blocks of sector 2 (and of sector 33 of a 4K) with other bytes, reads them back and puts the old bytes back.</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task ASector_IsWritten_ReadBack_AndPutBack() => Run((device, size, who) => ClassicScenarios.ASectorIsWrittenReadBackAndPutBackAsync(device, size, who));

        /// <summary>
        /// Makes a MAD on a card that has none, writes an application through it, reads it back through it, and puts the card back as it was
        /// (the keys of the MAD sectors are A0A1A2A3A4A5 and B0B1B2B3B4B5 for the time that the case runs: see <c>CLASSIC.md</c> if it stops half way).
        /// </summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task AnApplication_IsWrittenThroughTheMad_ReadBack_AndPutBack() => Run((device, size, who) => ClassicScenarios.AnApplicationIsWrittenThroughTheMadAndPutBackAsync(device, size, who));

        // Every test gets the reader that the fixture found, on the STA thread the reader SDKs need, the size of the card on it, and a way to ask
        // who is on the reader now (the cases that write ask, so that a card that was replaced meanwhile is not written to).
        private Task Run(Func<ReaderDevice, ClassicCardSize, Func<Task<string?>>, Task> scenario) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var host = _fixture.NewHost();

                var device = ReaderDevice.Instance ?? throw SkipException.ForSkip("No reader.");
                var error = await device.ReadChipPublic();

                if (error != ERROR.NoError || device.GenericChip == null || !ClassicScenarios.IsClassic(device.GenericChip.CardType, out var size))
                {
                    throw SkipException.ForSkip("There is no MIFARE Classic card on the reader. Put a Classic card as it left the factory on it.");
                }

                async Task<string?> WhoIsOnTheReaderAsync()
                {
                    var asked = await device.ReadChipPublic();

                    return asked == ERROR.NoError ? device.GenericChip?.UID : null;
                }

                await scenario(device, size, WhoIsOnTheReaderAsync);
            });
    }

    /// <summary>The collection of the Classic cases: one reader, one card, one case at a time.</summary>
    [CollectionDefinition("HILClassic")]
    public sealed class HilClassicCollection : ICollectionFixture<HilReaderFixture> { }
}
