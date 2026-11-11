using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>What the page's services do with the report opener: they ask it without holding the caller up.</summary>
    public sealed class GuidedPageServicesTests
    {
        // An opener that does not answer until the test lets it, as a share that is slow to answer does not.
        private sealed class BlockingOpener : IReportOpener
        {
            private readonly TaskCompletionSource<bool> _release = new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> _entered = new TaskCompletionSource<bool>();

            public string? Path { get; private set; }

            /// <summary>Completes when the opener has been called and is waiting on its share.</summary>
            public Task Entered => _entered.Task;

            public Result Answer { get; set; } = Result.Ok();

            public void Release() => _release.SetResult(true);

            public Result Open(string path)
            {
                Path = path;
                _entered.TrySetResult(true);
                _release.Task.GetAwaiter().GetResult();

                return Answer;
            }
        }

        [Fact]
        public async Task TheViewerIsAsked_WithoutHoldingTheCallerUp_AndARefusalIsTheResult()
        {
            var opener = new BlockingOpener { Answer = Result.Fail(new Failure(FailureKind.NotFound, OpenerFailureCodes.NoViewer, "No program.")) };
            var services = new GuidedPageServices(opener);

            var pending = services.OpenReportAsync(@"C:\reports\a.pdf", CancellationToken.None);

            // The opener is still blocked and the caller already has its task back.
            Assert.False(pending.IsCompleted);
            opener.Release();
            var failure = await pending.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(OpenerFailureCodes.NoViewer, failure!.Code);
            Assert.Equal(@"C:\reports\a.pdf", opener.Path);
        }

        [Fact]
        public async Task ACancelledWait_EndsAtOnce_WhileTheOpenerIsStillBlocked()
        {
            var opener = new BlockingOpener();
            using var cancel = new CancellationTokenSource();
            var pending = new GuidedPageServices(opener).OpenReportAsync(@"C:\reports\a.pdf", cancel.Token);

            await opener.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            cancel.Cancel();

            // The opener is still blocked on its share, and nobody waits for it any more.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
            opener.Release();
        }

        [Fact]
        public async Task AnOpenedReport_IsNoFailure()
        {
            var opener = new BlockingOpener();
            opener.Release();

            Assert.Null(await new GuidedPageServices(opener).OpenReportAsync(@"C:\reports\a.pdf", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public void TheServices_NeedAnOpener()
        {
            Assert.Throws<ArgumentNullException>(() => new GuidedPageServices(null!));
        }
    }
}
