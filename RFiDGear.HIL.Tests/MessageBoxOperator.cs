using System.Threading.Tasks;
using System.Windows;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Asks the person at the reader with a message box that is always on top, so that it is not hidden behind the console
    /// that runs the tests. It must be called on the STA thread of the test.
    /// </summary>
    internal sealed class MessageBoxOperator : IReaderOperator
    {
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
    }
}
