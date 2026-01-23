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
            var command = new AsyncRelayCommand(() => Task.FromException(new InvalidOperationException("Boom")));

            command.Execute(null);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(1)));

            Assert.Equal("Boom", exception.Message);
        }
    }
}
