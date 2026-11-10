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
            // Looks at the reader as often as the real operator would in the time, but without the pauses.
            private const int Looks = 25;

            public List<string> Asked { get; } = new List<string>();

            public List<string> Waited { get; } = new List<string>();

            // The answer to a message box (the step the reader cannot see): false is Cancel.
            public bool Confirms { get; set; } = true;

            // Whether the person keeps watching in the window (the steps the reader can see): false is Cancel.
            public bool Watches { get; set; } = true;

            public Task<bool> ConfirmAsync(string instruction)
            {
                Asked.Add(instruction);

                return Task.FromResult(Confirms);
            }

            public async Task<WaitOutcome> WaitAsync(string instruction, Func<Task<bool>> isDone, TimeSpan timeout)
            {
                Waited.Add(instruction);
                if (!Watches)
                {
                    return WaitOutcome.Cancelled;
                }

                for (var look = 0; look < Looks; look++)
                {
                    if (await isDone())
                    {
                        return WaitOutcome.Done;
                    }
                }

                return WaitOutcome.TimedOut;
            }
        }

        // Scripts the same answer many times: a card that does not go away.
        private static void Stays(ScriptedReadProvider reader, string uid)
        {
            for (var i = 0; i < 40; i++)
            {
                reader.Script(ERROR.NoError, Card(uid));
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
        public async Task ARemovedCard_IsNoticedByWatching_NobodyHasToClick()
        {
            var person = new ScriptedPerson();

            await Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));   // the card is seen
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));   // still there while the person reaches for it
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);                                     // gone
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, person));

            Assert.Equal(2, person.Waited.Count);
            Assert.Empty(person.Asked);
        }

        [Fact]
        public async Task ATroubledScanOnTheWay_ChangesNothingAndIsNotCountedAsAChange()
        {
            await Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, failure: new InvalidOperationException("the reader failed"));   // no answer about the card
                    NoCard(reader);
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson()));
        }

        [Fact]
        public async Task ACardThatIsPlacedLate_IsWaitedFor_BeforeTheRemovalIsAsked()
        {
            var person = new ScriptedPerson();

            await Run(
                reader =>
                {
                    NoCard(reader);                                     // the person is still looking for a card
                    NoCard(reader);
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, person));

            Assert.Equal(2, person.Waited.Count);
        }

        [Fact]
        public async Task AReaderThatNeverSeesACard_FailsTheRemovalCase_AtThePlacement()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    for (var i = 0; i < 40; i++)
                    {
                        NoCard(reader);
                    }
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson())));

            Assert.Contains("Gave up", failure.Message, StringComparison.Ordinal);
            Assert.Contains("a card on the reader", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACardThatStaysOnTheReader_FailsTheRemovalCase_AndSaysWhichCard()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader => Stays(reader, "04AABBCCDD"),
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson())));

            Assert.Contains("Gave up", failure.Message, StringComparison.Ordinal);
            Assert.Contains("the reader to be empty", failure.Message, StringComparison.Ordinal);
            Assert.Contains("DESFireEV1 04AABBCCDD", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ARemovedCardThatIsReportedAgainByAnotherCard_NeverMakesTheReaderEmpty()
        {
            // A second card near the antenna: the first one goes, the reader sees the other.
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    for (var i = 0; i < 40; i++)
                    {
                        reader.Script(ERROR.NoError, Card("04112233FF"));
                    }
                },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson())));

            Assert.Contains("04112233FF", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public Task AReplacedCard_IsReportedWithItsOwnUid() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04112233FF"));
                },
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson()));

        [Fact]
        public Task AReplacedCard_IsAnnouncedOncePerChange_WhenTheReaderSawItEmptyOnTheWay() =>
            Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);                                     // the person was slow: the reader saw it empty
                    reader.Script(ERROR.NoError, Card("04112233FF"));
                },
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson()));

        [Fact]
        public async Task TheSameCardPutBack_FailsTheReplacementCase()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader => Stays(reader, "04AABBCCDD"),
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson())));

            Assert.Contains("Gave up", failure.Message, StringComparison.Ordinal);
            Assert.Contains("the same card does not count", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ACardThatIsNotMifare_LeavesNoTraceOfTheOldCard_AndTheSwapIsClicked()
        {
            var person = new ScriptedPerson();

            await Run(
                reader =>
                {
                    reader.Script(ERROR.NoError, Card("04AABBCCDD"));
                    NoCard(reader);   // the new card is of a kind the application has no model for: not listed
                },
                host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, person));

            // The placement is watched, the swap is one move that the reader cannot see: one click, no second watching.
            Assert.Single(person.Waited);
            Assert.Single(person.Asked);
        }

        [Fact]
        public async Task APersonWhoCancelsTheBlindSwap_EndsTheNonMifareCase_AndSaysSo()
        {
            var person = new ScriptedPerson { Confirms = false };

            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                reader => reader.Script(ERROR.NoError, Card("04AABBCCDD")),
                host => ReaderScenarios.ACardThatIsNotMifareDoesNotLeaveTheOldOneBehindAsync(host, person)));

            Assert.Contains("cancelled", failure.Message, StringComparison.Ordinal);
            Assert.Single(person.Asked);
        }

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
        public async Task APersonWhoCancelsTheWaiting_EndsTheReplacementCase_AndSaysSo()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                _ => { },
                host => ReaderScenarios.AReplacedCardIsReportedWithItsOwnUidAsync(host, new ScriptedPerson { Watches = false })));

            Assert.Contains("cancelled", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task APersonWhoCancelsTheWaiting_EndsTheRemovalCase_AndSaysSo()
        {
            var failure = await Assert.ThrowsAsync<XunitException>(() => Run(
                _ => { },
                host => ReaderScenarios.ARemovedCardIsReportedAsGoneAsync(host, new ScriptedPerson { Watches = false })));

            Assert.Contains("cancelled", failure.Message, StringComparison.Ordinal);
        }
    }
}
