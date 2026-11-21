#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using RFiDGear.Bridge.SampleClient;

using Xunit;

namespace RFiDGear.Tests.ExternalControl
{
    /// <summary>
    /// The record of a card whose decision is under way. What matters is how its decision is dropped: the token is cancelled at once,
    /// so that a decision that finishes meanwhile sends nothing, and the callbacks of the program run elsewhere, so that one that blocks
    /// or throws holds nothing up.
    /// </summary>
    public sealed class PendingCardTests
    {
        [Fact]
        public void ADroppedDecision_IsCancelledWhenDropReturns_ThoughACallbackOfTheProgramBlocks()
        {
            using var gate = new ManualResetEventSlim(false);

            try
            {
                // A queued cancellation would leave the token alone for a moment now and then; ask often enough to see that.
                for (var attempt = 0; attempt < 200; attempt++)
                {
                    var cancel = new CancellationTokenSource();
                    cancel.Token.Register(() => gate.Wait());

                    new PendingCard("04AA", cancel, Task.FromResult("none")).Drop();

                    Assert.True(cancel.Token.IsCancellationRequested, "The token was not cancelled when Drop returned (attempt " + attempt + ").");
                }
            }
            finally
            {
                gate.Set();
            }
        }

        [Fact]
        public async Task ASourceIsLetGoOfOnceTheCallbacksHaveRun_NotBefore()
        {
            using var gate = new ManualResetEventSlim(false);
            var cancel = new CancellationTokenSource();
            cancel.Token.Register(() => gate.Wait());

            new PendingCard("04AA", cancel, Task.FromResult("none")).Drop();
            await Task.Delay(200);

            Assert.False(IsDisposed(cancel), "The source was let go of while a callback was still running.");

            gate.Set();

            var until = DateTime.UtcNow.AddSeconds(5);

            while (!IsDisposed(cancel))
            {
                Assert.True(DateTime.UtcNow < until, "The source was not let go of after the callbacks had run.");
                await Task.Delay(10);
            }
        }

        [Fact]
        public async Task ACallbackThatThrows_DoesNotSurface_AndTheSourceIsLetGoOfAll()
        {
            var cancel = new CancellationTokenSource();
            cancel.Token.Register(() => throw new InvalidOperationException("The callback of the decision threw."));
            var card = new PendingCard("04AA", cancel, Task.FromResult("none"));

            Assert.Null(Record.Exception(card.Drop));
            Assert.True(cancel.Token.IsCancellationRequested);

            var until = DateTime.UtcNow.AddSeconds(5);

            while (!IsDisposed(cancel))
            {
                Assert.True(DateTime.UtcNow < until, "The source was not let go of.");
                await Task.Delay(10);
            }

            // Doing it twice does nothing.
            Assert.Null(Record.Exception(card.Drop));
        }

        private static bool IsDisposed(CancellationTokenSource source)
        {
            try
            {
                _ = source.Token.WaitHandle;

                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }
    }
}
