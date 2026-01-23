using System;
using System.Threading.Tasks;
using RFiDGear.Infrastructure;
using Xunit;

namespace RFiDGear.Tests
{
    public class AsyncOperationLockTests
    {
        [Fact]
        public async Task AcquireAsync_SerializesAccess()
        {
            var operationLock = new AsyncOperationLock();
            var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var firstTask = Task.Run(async () =>
            {
                using (await operationLock.AcquireAsync())
                {
                    firstEntered.TrySetResult(true);
                    await releaseFirst.Task;
                }
            });

            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            var secondTask = Task.Run(async () =>
            {
                using (await operationLock.AcquireAsync())
                {
                    secondEntered.TrySetResult(true);
                }
            });

            await Task.Delay(50);
            Assert.False(secondEntered.Task.IsCompleted);

            releaseFirst.TrySetResult(true);

            await Task.WhenAll(firstTask, secondTask);
            Assert.True(secondEntered.Task.IsCompleted);
        }
    }
}
