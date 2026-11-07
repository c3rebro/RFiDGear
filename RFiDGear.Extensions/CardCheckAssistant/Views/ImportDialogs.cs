using System.Linq;
using System.Windows;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant.Services;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The import questions as Windows dialogs. "Not now" is what the Escape key and the close button mean, and what
    /// the Enter key does: nothing is imported, and nothing is remembered, unless the operator picks a button.
    /// </summary>
    internal sealed class ImportDialogs : IImportDialogs
    {
        /// <inheritdoc/>
        public ImportAnswer Ask(string title, string text, string importButton, string notNowButton, string neverButton)
        {
            var answer = ImportAnswer.NotNow;
            var window = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = ActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            Button AddButton(Panel row, string content, ImportAnswer value, bool safe)
            {
                var button = new Button { Content = content, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3), IsDefault = safe, IsCancel = safe };
                button.Click += (_, _) =>
                {
                    answer = value;
                    window.Close();
                };
                row.Children.Add(button);

                return button;
            }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            AddButton(buttons, importButton, ImportAnswer.Import, safe: false);
            AddButton(buttons, notNowButton, ImportAnswer.NotNow, safe: true);
            AddButton(buttons, neverButton, ImportAnswer.Never, safe: false);

            var content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
            content.Children.Add(buttons);
            window.Content = content;

            window.ShowDialog();

            return answer;
        }

        /// <inheritdoc/>
        public bool Confirm(string title, string text)
        {
            var owner = ActiveWindow();

            return owner == null
                ? MessageBox.Show(text, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK
                : MessageBox.Show(owner, text, title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;
        }

        private static Window? ActiveWindow() =>
            Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
    }
}
