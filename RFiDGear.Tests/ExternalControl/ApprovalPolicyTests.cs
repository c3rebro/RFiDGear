#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge;
using RFiDGear.Bridge.SampleClient;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The options of the command <c>auto</c> of the sample client that make it wait for approval. What matters most is what they
    /// refuse: an option that is not understood must never end up as the plain automatic mode, which runs every card.
    /// </summary>
    public sealed class ApprovalPolicyTests
    {
        private static readonly CardPresentedEvent Shown = new CardPresentedEvent { PresenceId = "p-1", Uid = "04AA", ProjectRevision = "rev-1" };

        private static string[] Split(string line) => line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        [Theory]
        [InlineData("")]
        [InlineData("--approve-with")]
        [InlineData("--approve-with $EXPECTEDUID")]
        [InlineData("--aprove-with=$EXPECTEDUID")]
        [InlineData("--no-pin")]
        [InlineData("--wait=5")]
        [InlineData("--set=A=b")]
        [InlineData("--decline --no-answer")]
        [InlineData("--decline --approve-with=$X")]
        [InlineData("--decline --no-pin")]
        [InlineData("--no-answer --wait=3")]
        [InlineData("--approve-with=$X --approve-with=$Y")]
        [InlineData("--approve-with=$X extra")]
        [InlineData("--approve-with=X")]
        [InlineData("--approve-with=$")]
        [InlineData("--approve-with=$X --wait=abc")]
        [InlineData("--approve-with=$X --wait=601")]
        [InlineData("--approve-with=$X --wait=-1")]
        [InlineData("--approve-with=$X --wait=NaN")]
        [InlineData("--approve-with=$X --wait=Infinity")]
        [InlineData("--approve-with=$X --wait=-Infinity")]
        [InlineData("--approve-with=$X --set=novalue")]
        [InlineData("--approve-with=$X --set==x")]
        public void Options_ThatDoNotMakeExactlyOneRule_AreRefused_AndNeverBecomeAnyModeAtAll(string line)
        {
            Assert.False(ApprovalPolicy.TryRead(Split(line), out var decide, out _, out var error));
            Assert.Null(decide);
            Assert.NotEmpty(error);
        }

        [Fact]
        public async Task Decline_DeclinesEveryCard()
        {
            Assert.True(ApprovalPolicy.TryRead(Split("--decline"), out var decide, out var description, out _));

            Assert.False((await decide!(Shown, CancellationToken.None)).Approve);
            Assert.NotEmpty(description);
        }

        [Fact]
        public async Task NoAnswer_DoesNotAnswer_UntilItsCardIsGivenUp()
        {
            Assert.True(ApprovalPolicy.TryRead(Split("--no-answer"), out var decide, out _, out _));
            using var cardGivenUp = new CancellationTokenSource();

            var deciding = decide!(Shown, cardGivenUp.Token);
            await Task.Delay(50);

            Assert.False(deciding.IsCompleted);

            cardGivenUp.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => deciding);
        }

        [Fact]
        public async Task Approve_SetsTheVariableToTheUidAndHoldsTheProjectToTheStateTheCardWasShownWith()
        {
            Assert.True(ApprovalPolicy.TryRead(Split("--approve-with=$EXPECTEDUID --set=REPORTTARGETPATH=C:\\reports\\card-??.pdf"), out var decide, out var description, out _));

            var decision = await decide!(Shown, CancellationToken.None);

            Assert.True(decision.Approve);
            Assert.True(decision.PinRevision);
            Assert.Equal("04AA", decision.Variables!["$EXPECTEDUID"]);
            Assert.Equal("C:\\reports\\card-??.pdf", decision.Variables["REPORTTARGETPATH"]);
            Assert.Contains("$EXPECTEDUID", description);
        }

        [Fact]
        public async Task Approve_LeavesTheStateOfTheProjectOut_OnlyWhenToldTo()
        {
            Assert.True(ApprovalPolicy.TryRead(Split("--approve-with=$EXPECTEDUID --no-pin"), out var decide, out _, out _));

            Assert.False((await decide!(Shown, CancellationToken.None)).PinRevision);
        }

        [Fact]
        public async Task Approve_ThinksFirst_AndThinksNoMoreThanItsCardIsWaitedFor()
        {
            Assert.True(ApprovalPolicy.TryRead(Split("--approve-with=$EXPECTEDUID --wait=60"), out var decide, out var description, out _));
            using var cardGivenUp = new CancellationTokenSource();
            cardGivenUp.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decide!(Shown, cardGivenUp.Token));
            Assert.Contains("60", description);
        }

        [Theory]
        [InlineData("--approve-with")]
        [InlineData("--approve-with $EXPECTEDUID")]
        [InlineData("--no-pin")]
        [InlineData("--decline")]
        [InlineData("anything")]
        public void OnlyABareAuto_IsThePlainMode_ThatRunsEveryCard(string line)
        {
            Assert.True(ApprovalPolicy.IsPlainMode(Array.Empty<string>()));
            Assert.False(ApprovalPolicy.IsPlainMode(Split(line)));
        }

        [Fact]
        public void TheOptionsAreTheOnesTheCommandKnows()
        {
            var known = new[] { "--approve-with=$X", "--decline", "--no-answer", "--no-pin", "--set=A=b", "--wait=5" };

            Assert.All(known, option => Assert.True(ApprovalPolicy.IsOption(option)));
            Assert.False(ApprovalPolicy.IsOption("--approve-with"));
            Assert.False(ApprovalPolicy.IsOption("--expect-revision=x"));
            Assert.Equal(known.Length, known.Count(ApprovalPolicy.IsOption));
        }
    }
}
