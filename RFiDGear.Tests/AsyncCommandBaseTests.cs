using System;
using System.Threading.Tasks;
using System.Windows.Input;
using RFiDGear.DataAccessLayer;
using Xunit;

namespace RFiDGear.Tests
{
    public class AsyncCommandBaseTests
    {
        [Fact]
        public async Task ICommandExecute_UsesExceptionHandlerForFaultedTask()
        {
            var command = new TestAsyncCommand();

            ((ICommand)command).Execute(null);

            var exception = await command.ExceptionTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsType<InvalidOperationException>(exception);
        }

        private sealed class TestAsyncCommand : AsyncCommandBase
        {
            private readonly TaskCompletionSource<Exception> _exceptionSource = new();

            public Task<Exception> ExceptionTask => _exceptionSource.Task;

            public override bool CanExecute(object parameter)
            {
                return true;
            }

            public override Task ExecuteAsync(object parameter)
            {
                return Task.FromException(new InvalidOperationException("Boom"));
            }

            protected override void HandleExecutionException(Exception exception)
            {
                _exceptionSource.TrySetResult(exception);
            }
        }
    }
}
