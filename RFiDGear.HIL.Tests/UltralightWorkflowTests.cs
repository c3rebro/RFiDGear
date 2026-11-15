#nullable enable
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;

using System;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The Ultralight / NTAG task against a real reader and a real card (see <c>ULTRALIGHT.md</c>): reading pages, a chip that is not
    /// the one expected, a write to a user page that is put back, and a write to a protected page that is refused. The steps are in
    /// <see cref="UltralightScenarios"/>, which the unit tests also run against fakes of the reader in memory; this class only
    /// supplies the reader and the card.
    /// <para>
    /// They need the reader-scan tests (<c>readerScan</c> in <c>hil.settings.json</c>). The two that write to the card also need
    /// <c>cardWrites</c>. Which reader is used is <c>readerKind</c>. No case sends a key or a password to a card.
    /// </para>
    /// </summary>
    [Collection("HILUltralight")]
    public sealed class UltralightWorkflowTests
    {
        private readonly HilReaderFixture _fixture;

        /// <summary>Creates the cases with the reader that was found.</summary>
        /// <param name="fixture">The reader.</param>
        public UltralightWorkflowTests(HilReaderFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// The first user pages are read with the chip left open and with it named. An Elatec reader gives them for every chip of the
        /// family; a PC/SC reader only for the plain Ultralight and the Ultralight C, and refuses the others.
        /// </summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AReadOfTheUserPages_IsAnswered_AsTheReaderAndTheChipAllow() => Run(UltralightScenarios.AReadOfTheUserPagesAsync);

        /// <summary>A task that names another chip than the one on the reader reads nothing.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AnExpectationThatIsNotTheChip_IsRefused_AndNothingIsRead() => Run(UltralightScenarios.AnExpectationThatIsNotTheChipIsRefusedAsync);

        /// <summary>Writes page 10 with other bytes, reads it back and puts the old bytes back.</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task AWriteToAUserPage_IsReadBack_AndPutBack() => Run(UltralightScenarios.AWriteToAUserPageIsReadBackAndPutBackAsync);

        /// <summary>A write to page 3 is refused before the card is asked (zeros: nothing could change even if it were not).</summary>
        /// <returns>The task of the test.</returns>
        [HilCardWriteFact]
        public Task AWriteToAProtectedPage_IsRefused_BeforeTheCard() => Run(UltralightScenarios.AWriteToAProtectedPageIsRefusedBeforeTheCardAsync);

        // Every test gets the reader that the fixture found, on the STA thread the reader SDKs need, and the type of the card on it.
        private Task Run(Func<CARD_TYPE, Task> scenario) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var host = _fixture.NewHost();

                var device = ReaderDevice.Instance ?? throw SkipException.ForSkip("No reader.");
                var error = await device.ReadChipPublic();

                if (error != ERROR.NoError || device.GenericChip == null || device.GenericChip.CardType == CARD_TYPE.NOTAG)
                {
                    throw SkipException.ForSkip("There is no card on the reader. Put an Ultralight, Ultralight C, Ultralight EV1 or NTAG card on it.");
                }

                await scenario(device.GenericChip.CardType);
            });
    }

    /// <summary>The collection of the Ultralight cases: one reader, one card, one case at a time.</summary>
    [CollectionDefinition("HILUltralight")]
    public sealed class HilUltralightCollection : ICollectionFixture<HilReaderFixture> { }
}
