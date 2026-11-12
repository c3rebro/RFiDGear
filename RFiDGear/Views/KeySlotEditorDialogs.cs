using System;
using System.Globalization;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>The questions of the key slot editor, asked with message boxes and a small window.</summary>
    internal sealed class WpfKeySlotEditorDialogs : IKeySlotEditorDialogs
    {
        private readonly Func<Window> _owner;

        /// <summary>Creates the dialogs.</summary>
        /// <param name="owner">Gets the window that the dialogs belong to; may give <see langword="null"/>.</param>
        internal WpfKeySlotEditorDialogs(Func<Window> owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <inheritdoc />
        public bool ConfirmReplaceKey() =>
            Ask(ResourceLoader.GetResource("messageKeySlotReplaceKey"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        /// <inheritdoc />
        public SecureString AskForKey(int hexLength)
        {
            var window = new StoreKeyWindow(hexLength);
            var owner = _owner();

            if (owner != null)
            {
                window.Owner = owner;
            }

            return window.ShowDialog() == true ? window.Entered : null;
        }

        /// <inheritdoc />
        public void Show(KeySlotMessage message, int detail)
        {
            switch (message)
            {
                case KeySlotMessage.KeyStored:
                    Ask(ResourceLoader.GetResource("messageKeySlotKeyStored"), MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case KeySlotMessage.KeyNotStored:
                    Ask(string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("messageKeySlotKeyNotStored"), detail), MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
                default:
                    Ask(string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("messageKeySlotKeyInvalid"), detail), MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }

        private MessageBoxResult Ask(string text, MessageBoxButton buttons, MessageBoxImage image)
        {
            var caption = ResourceLoader.GetResource("captionKeySlot");
            var owner = _owner();

            return owner != null
                ? MessageBox.Show(owner, text, caption, buttons, image)
                : MessageBox.Show(text, caption, buttons, image);
        }
    }

    /// <summary>
    /// Asks for a key to store. The digits are typed into a password box and handed on as a secure string; they are not
    /// shown and not kept.
    /// </summary>
    internal sealed class StoreKeyWindow : Window
    {
        private readonly PasswordBox _box = new PasswordBox();

        /// <summary>Creates the window.</summary>
        /// <param name="hexLength">How many digits the key has.</param>
        internal StoreKeyWindow(int hexLength)
        {
            Title = ResourceLoader.GetResource("windowTitleKeySlotStoreKey");
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _box.MaxLength = hexLength;
            _box.MinWidth = 320;

            var ok = new Button
            {
                Content = ResourceLoader.GetResource("buttonContentKeySlotStoreKeyOk"),
                IsDefault = true,
                MinWidth = 80,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(6, 2, 6, 2),
            };
            ok.Click += (_, _) =>
            {
                Entered = _box.SecurePassword;
                _box.Clear();
                DialogResult = true;
            };

            var cancel = new Button
            {
                Content = ResourceLoader.GetResource("buttonContentKeySlotStoreKeyCancel"),
                IsCancel = true,
                MinWidth = 80,
                Padding = new Thickness(6, 2, 6, 2),
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("labelKeySlotStoreKeyPrompt"), hexLength),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 380,
                Margin = new Thickness(0, 0, 0, 8),
            });
            panel.Children.Add(_box);
            panel.Children.Add(buttons);

            Content = panel;
            Loaded += (_, _) => _box.Focus();
        }

        /// <summary>Gets what was typed, after the window closed with OK; the caller disposes it.</summary>
        internal SecureString Entered { get; private set; }

        /// <inheritdoc />
        protected override void OnClosed(EventArgs e)
        {
            _box.Clear();
            base.OnClosed(e);
        }
    }
}
