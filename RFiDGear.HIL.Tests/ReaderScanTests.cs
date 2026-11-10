using RFiDGear.Contracts;
using RFiDGear.Infrastructure.ExtensionHost;

using System;
using System.Threading.Tasks;

using Xunit;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// The reader scan of the extension host (<see cref="IReaderHost.ScanAsync"/>) against a real reader. The steps of each
    /// case are in <see cref="ReaderScenarios"/>, which the unit tests also run against a scripted reader; this class only
    /// supplies the reader and the person.
    /// <para>
    /// Without <c>hil.settings.json</c> (or with <c>readerScan</c> off) every test is reported as skipped and nothing
    /// touches the reader. The cases that ask for cards to be moved also need <c>interactive</c> on. See <c>README.md</c>
    /// of this project.
    /// </para>
    /// <para>
    /// The cases share one reader and one card, so their order is fixed (see <see cref="HilCaseOrderer"/>): the cases that
    /// do not move cards run first, with the card that was on the reader when the tests started, and the cases that ask
    /// for cards run last, each of them asking for the card it needs.
    /// </para>
    /// </summary>
    [Collection("HILReader")]
    [TestCaseOrderer("RFiDGear.HIL.Tests.HilCaseOrderer", "RFiDGear.HIL.Tests")]
    public sealed class ReaderScanTests
    {
        private readonly HilReaderFixture _fixture;

        /// <summary>Creates the test class for one test.</summary>
        /// <param name="fixture">The reader that was found.</param>
        public ReaderScanTests(HilReaderFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>Smoke test: a scan always answers. Any reader; with a PC/SC reader a card must be on it at the start.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AScan_AlwaysAnswers() => Run(ReaderScenarios.AScanAlwaysAnswersAsync);

        /// <summary>Needs a card on the reader from the start.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task ACardOnTheReader_IsReportedWithItsUidAndType() => Run(ReaderScenarios.ACardOnTheReaderIsReportedAsync);

        /// <summary>Needs a card on the reader from the start; do not move it during the test.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task ASecondScanOfTheSameCard_RaisesNoEvent() => Run(ReaderScenarios.ASecondScanOfTheSameCardRaisesNothingAsync);

        /// <summary>Works with or without a card on an Elatec reader; a PC/SC reader needs a card at the start.</summary>
        /// <returns>The task of the test.</returns>
        [HilReaderFact]
        public Task AScanDuringARun_IsAnsweredBusy() => Run(ReaderScenarios.AScanDuringARunIsAnsweredBusyAsync);

        /// <summary>Asks for a card to be put on the reader and taken off.</summary>
        /// <returns>The task of the test.</returns>
        [HilInteractiveFact]
        public Task ARemovedCard_IsReportedAsGone() =>
            Run(host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new MessageBoxOperator()));

        /// <summary>Asks for two different cards.</summary>
        /// <returns>The task of the test.</returns>
        [HilInteractiveFact]
        public Task AReplacedCard_IsReportedWithItsOwnUid() =>
            Run(host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new MessageBoxOperator()));

        /// <summary>
        /// Asks for a MIFARE card and then for an HF card that is not a MIFARE card; this is the Elatec fix of the reader scan,
        /// which cannot be tested without a reader.
        /// </summary>
        /// <returns>The task of the test.</returns>
        [HilInteractiveFact]
        public Task ACardThatIsNotMifare_DoesNotLeaveTheOldCardBehind() =>
            Run(host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, new MessageBoxOperator()));

        // Every test gets a host that has not scanned yet, on the STA thread the reader SDKs and the message box need.
        private Task Run(Func<HeadlessExtensionHost, Task> scenario) =>
            StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                using var host = _fixture.NewHost();

                await scenario(host);
            });
    }
}
