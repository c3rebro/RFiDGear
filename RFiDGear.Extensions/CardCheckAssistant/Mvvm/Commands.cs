using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.Extensions.CardCheckAssistant.Mvvm
{
    /// <summary>A command that runs a delegate. Call <see cref="RaiseCanExecuteChanged"/> when its condition changes.</summary>
    public sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        /// <param name="execute">What the command does.</param>
        /// <param name="canExecute">Whether it can run now; always when omitted.</param>
        public DelegateCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc/>
        public event EventHandler? CanExecuteChanged;

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        /// <inheritdoc/>
        public void Execute(object? parameter)
        {
            if (CanExecute(parameter))
            {
                _execute();
            }
        }

        /// <summary>Tells the bound control to ask <see cref="CanExecute"/> again.</summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A command that runs an asynchronous delegate and cannot start again while the previous run is still going.
    /// Nothing the delegate throws can reach the dispatcher: a failure it did not handle itself is passed to the
    /// <c>onUnhandled</c> callback instead of ending the host.
    /// </summary>
    public sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private readonly Action<Exception>? _onUnhandled;
        private bool _isRunning;

        /// <param name="execute">What the command does.</param>
        /// <param name="canExecute">Whether it can run now, besides not running already; always when omitted.</param>
        /// <param name="onUnhandled">Receives an exception the delegate did not handle itself.</param>
        public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onUnhandled = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onUnhandled = onUnhandled;
        }

        /// <inheritdoc/>
        public event EventHandler? CanExecuteChanged;

        /// <inheritdoc/>
        public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

        /// <inheritdoc/>
        public void Execute(object? parameter) => _ = ExecuteAsync();

        /// <summary>
        /// Runs the command and completes when it has finished. It never throws: see <c>onUnhandled</c>. Does nothing
        /// when the command cannot run now.
        /// </summary>
        /// <returns>A task that completes when the delegate has finished.</returns>
        public async Task ExecuteAsync()
        {
            if (!CanExecute(null))
            {
                return;
            }

            _isRunning = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                _onUnhandled?.Invoke(ex);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>Tells the bound control to ask <see cref="CanExecute"/> again.</summary>
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
