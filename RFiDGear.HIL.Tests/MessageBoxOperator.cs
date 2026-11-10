using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Asks the person at the reader with windows that are always on top, so that they are not hidden behind the console
    /// that runs the tests: a message box for a step the reader cannot see, and a window with a Cancel button that stays
    /// while the test watches the reader for a step it can see. It must be called on the STA thread of the test.
    /// </summary>
    internal sealed class MessageBoxOperator : IReaderOperator
    {
        // The pause between two looks at the reader; a look takes a second or more by itself.
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        /// <inheritdoc/>
        public Task<bool> ConfirmAsync(string instruction)
        {
            var owner = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Topmost = true
            };
            owner.Show();

            try
            {
                var answer = MessageBox.Show(
                    owner,
                    instruction,
                    "RFiDGear hardware test",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information,
                    MessageBoxResult.OK);

                return Task.FromResult(answer == MessageBoxResult.OK);
            }
            finally
            {
                owner.Close();
            }
        }

        /// <inheritdoc/>
        public async Task<WaitOutcome> WaitAsync(string instruction, Func<Task<bool>> isDone, TimeSpan timeout)
        {
            var cancelled = false;
            var status = new TextBlock { Margin = new Thickness(0, 10, 0, 10), Foreground = Brushes.DimGray };
            var cancel = new Button { Content = "Cancel", Width = 90, HorizontalAlignment = HorizontalAlignment.Right };
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = instruction, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(status);
            panel.Children.Add(cancel);

            var window = new Window
            {
                Title = "RFiDGear hardware test",
                Topmost = true,
                Width = 480,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = panel
            };
            cancel.Click += (_, _) => cancelled = true;
            window.Closing += (_, _) => cancelled = true;
            window.Show();

            try
            {
                var deadline = DateTime.UtcNow + timeout;
                while (!cancelled)
                {
                    if (await isDone())
                    {
                        return WaitOutcome.Done;
                    }

                    var left = deadline - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero)
                    {
                        return WaitOutcome.TimedOut;
                    }

                    status.Text = $"Watching the reader... {Math.Ceiling(left.TotalSeconds):0} s left";
                    await Task.Delay(PollInterval);
                }

                return WaitOutcome.Cancelled;
            }
            finally
            {
                window.Close();
            }
        }
    }
}
