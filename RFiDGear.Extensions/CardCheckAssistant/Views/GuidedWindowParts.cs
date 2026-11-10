using System;
using System.Windows;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.Services;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>Asks with a message box whether the window may be closed in the middle of a check.</summary>
    internal sealed class MessageBoxCloseConfirmation : ICloseConfirmation
    {
        /// <inheritdoc/>
        public bool Confirm(string title, string text)
        {
            var owner = ImportDialogs.ActiveWindow();
            var answer = owner == null
                ? MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                : MessageBox.Show(owner, text, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            return answer == MessageBoxResult.Yes;
        }
    }

    /// <summary>The repeating timer of the page, as a dispatcher timer: its tick runs on the window's thread.</summary>
    internal static class DispatcherTimerStarter
    {
        /// <summary>Makes the function the page starts its timer with.</summary>
        /// <param name="dispatcher">The dispatcher of the window.</param>
        /// <returns>A function that starts a timer and returns what stops it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
        public static Func<TimeSpan, Action, IDisposable> For(Dispatcher dispatcher)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            return (interval, tick) => new Running(dispatcher, interval, tick);
        }

        private sealed class Running : IDisposable
        {
            private readonly DispatcherTimer _timer;

            public Running(Dispatcher dispatcher, TimeSpan interval, Action tick)
            {
                // This constructor starts the timer.
                _timer = new DispatcherTimer(interval, DispatcherPriority.Background, (_, _) => tick(), dispatcher);
            }

            public void Dispose() => _timer.Stop();
        }
    }
}
