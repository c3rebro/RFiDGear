using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using RFiDGear.HIL.Tests;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Models;

using Xunit;
using Xunit.Sdk;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// The steps of the hardware cases (<c>RFiDGear.HIL.Tests/ReaderScenarios.cs</c>, compiled into this project too) run
    /// against a scripted reader, with the real host. A case that is wrong would otherwise be found only at the reader:
    /// here every case has to pass for the behaviour it describes, and most of them are shown to fail when the reader
    /// behaves otherwise. What a scripted reader cannot show, such as the Elatec fix itself, stays with the hardware.
    /// </summary>
    public class HilScenarioSelfCheckTests
    {
        private static GenericChipModel Card(string uid) => new GenericChipModel(uid, CARD_TYPE.DESFireEV1);

        // A read that finds no card: it succeeds and leaves the empty chip the host put there.
        private static void NoCard(ScriptedReadProvider reader) => reader.Script(ERROR.NoError);

        private sealed class ScriptedPerson : IReaderOperator
        {
            public List<string> Asked { get; } = new List<string>();

            public bool Confirms { get; set; } = true;

            public Task<bool> ConfirmAsync(string instruction)
            {
                Asked.Add(instruction);

                return Task.FromResult(Confirms);
            }
        }

        private static async Task Run(Action<ScriptedReadProvider> script, Func<HeadlessExtensionHost, Task> scenario)
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var reader = new ScriptedReadProvider();
                script(reader);

                using (ReaderDeviceScope.Use(reader))
                using (var host = new HeadlessExtensionHost("self-check"))
                {
                    await scenario(host);
                }
            });
        }

        [Fact]
        public async Task AScanAlwaysAnswers_WithACard_AndWithoutOne()
        {
            await Run(reader => reader.Script(ERROR.NoError, Card("04AABBCCDD")), ReaderScenarios.AScanAlwaysAnswersAsync);
            await Run(NoCard, ReaderScenarios.AScanAlwaysAnswersAsync);
        }

        [Fact]
        public async Task AScanThatFails_FailsTheSmokeCaseWithTheStateInTheMessage()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() =>
                Run(reader => reader.Script(ERROR.NoError, failure: new InvalidOperationException("the reader failed")), ReaderScenarios.AScanAlwaysAnswersAsync));

            Assert.Contains("ReadFailed", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task ACardOnTheReader_IsReported() =>
            Run(reader => reader.Script(ERROR.NoError, Card("04AABBCCDD")), ReaderScenarios.ACardOnTheReaderIsReportedAsync);

        [Fact]
        public async Task WithoutACard_TheCardCaseSaysWhatToDo()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(NoCard, ReaderScenarios.ACardOnTheReaderIsReportedAsync));

            Assert.Contains("Put a card", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task ASecondScanOfTheSameCard_RaisesNothing() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                },
                ReaderScenarios.ASecondScanOfTheSameCardRaisesNothingAsync);

        [Fact]
        public async Task ACardThatChangesBetweenTwoScans_FailsTheSecondScanCase()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04112233FF"));
                },
                ReaderScenarios.ASecondScanOfTheSameCardRaisesNothingAsync));

            Assert.Contains("another picture", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task AScanDuringARun_IsBusy_AndScansAgainAfterwards() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));   // the scan before the run
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));   // the run reads the card first
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));   // the scan after the run
                },
                ReaderScenarios.AScanDuringARunIsAnsweredBusyAsync);

        [Fact]
        public async Task ARemovedCard_IsReportedAsGone_AndTheCaseAsksTwice()
        {
            var person = new ScriptedPerson();

            await Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, person));

            Assert.Equal(2, person.Asked.Count);
        }

        [Fact]
        public async Task ACardThatStaysOnTheReader_FailsTheRemovalCase()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson())));

            Assert.Contains("still reported", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task AReplacedCard_IsReportedWithItsOwnUid() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04112233FF"));
                },
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson()));

        [Fact]
        public async Task TheSameCardPutBack_FailsTheReplacementCase()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                },
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson())));

            Assert.Contains("same UID", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task ACardThatIsNotMifare_LeavesNoTraceOfTheOldCard() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);   // the new card is of a kind the application has no model for: not listed
                },
                host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, new ScriptedPerson()));

        [Fact]
        public async Task AReplacementTheReaderCannotRead_FailsTheNonMifareCaseInsteadOfPassingIt()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, failure: new InvalidOperationException("the reader failed"));
                },
                host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, new ScriptedPerson())));

            Assert.Contains("not Ready", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnOldCardThatStaysBehind_FailsTheNonMifareCase()
        {
            // What the reader did before the fix: the tag of the read before is reported again.
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                },
                host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, new ScriptedPerson())));

            Assert.Contains("still reported", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task APersonWhoCancels_EndsTheCase_AndSaysSo()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                _ => { },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson { Confirms = false })));

            Assert.Contains("cancelled", failure.Message, StringComparison.Ordinal);
        }
    }
}
