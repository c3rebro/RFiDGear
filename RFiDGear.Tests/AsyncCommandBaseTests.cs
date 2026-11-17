using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace RFiDGear.Tests
{
    public class AsyncCommandBaseTests
    {
        [Fact]
        public async Task Execute_PropagatesExceptionsFromExecutionTask()
        {
            // FlowExceptionsToTaskScheduler: Execute does not rethrow the exception from an async void method as well, which
            // xunit.v3 would report as a fatal error of the run; the exception stays in ExecutionTask, which this test is about.
            var command = new AsyncRelayCommand(
                () => Task.FromException(new InvalidOperationException("Boom")),
                AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);

            command.Execute(null);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(1)));

            Assert.Equal("Boom", exception.Message);
        }
    }
}
