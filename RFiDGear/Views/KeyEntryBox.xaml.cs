using System;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// The box in which a key is entered in the key manager. It is hidden, like a password, until the person asks to see it; it
    /// takes no more characters than the key has digits; and it turns green for a key and red for anything else while it is typed,
    /// as the key boxes of the task dialogs do. A shown key is in a text box and so in a string, which cannot be erased: that is
    /// why the box starts hidden, is hidden again when it is cleared, and is cleared after every attempt to store a key.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class KeyEntryBox : UserControl
    {
        /// <summary>Identifies <see cref="HexLength"/>.</summary>
        public static readonly DependencyProperty HexLengthProperty = DependencyProperty.Register(
            nameof(HexLength), typeof(int), typeof(KeyEntryBox), new PropertyMetadata(32, OnHexLengthChanged));

        /// <summary>Identifies <see cref="Label"/>.</summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(KeyEntryBox), new PropertyMetadata(string.Empty));

        /// <summary>Identifies <see cref="ShowKeyText"/>.</summary>
        public static readonly DependencyProperty ShowKeyTextProperty = DependencyProperty.Register(
            nameof(ShowKeyText), typeof(string), typeof(KeyEntryBox), new PropertyMetadata(string.Empty));

        /// <summary>Identifies <see cref="ShowKeyToolTip"/>.</summary>
        public static readonly DependencyProperty ShowKeyToolTipProperty = DependencyProperty.Register(
            nameof(ShowKeyToolTip), typeof(string), typeof(KeyEntryBox), new PropertyMetadata(string.Empty, OnShowKeyToolTipChanged));

        /// <summary>Identifies <see cref="IsShown"/>.</summary>
        public static readonly DependencyProperty IsShownProperty = DependencyProperty.Register(
            nameof(IsShown), typeof(bool), typeof(KeyEntryBox), new PropertyMetadata(false, OnIsShownChanged));

        private static readonly DependencyPropertyKey IsKeyValidPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsKeyValid), typeof(bool), typeof(KeyEntryBox), new PropertyMetadata(false));

        /// <summary>Identifies <see cref="IsKeyValid"/>.</summary>
        public static readonly DependencyProperty IsKeyValidProperty = IsKeyValidPropertyKey.DependencyProperty;

        private bool _switching;

        /// <summary>Creates the box; it is hidden and empty, which is not a key.</summary>
        public KeyEntryBox()
        {
            InitializeComponent();
            Recompute();
        }

        /// <summary>Raised when Enter is pressed in either box; the key is stored by whoever listens, when it can be.</summary>
        internal event EventHandler EnterPressed;

        /// <summary>Gets or sets how many hexadecimal digits the key has; the boxes take no more characters than that.</summary>
        public int HexLength
        {
            get => (int)GetValue(HexLengthProperty);
            set => SetValue(HexLengthProperty, value);
        }

        /// <summary>Gets or sets the name that the boxes have for assistive technology.</summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>Gets or sets the text of the check box that shows the key.</summary>
        public string ShowKeyText
        {
            get => (string)GetValue(ShowKeyTextProperty);
            set => SetValue(ShowKeyTextProperty, value);
        }

        /// <summary>Gets or sets the tooltip of the check box that shows the key.</summary>
        public string ShowKeyToolTip
        {
            get => (string)GetValue(ShowKeyToolTipProperty);
            set => SetValue(ShowKeyToolTipProperty, value);
        }

        /// <summary>Gets or sets whether the digits are shown.</summary>
        public bool IsShown
        {
            get => (bool)GetValue(IsShownProperty);
            set => SetValue(IsShownProperty, value);
        }

        /// <summary>Gets whether what is entered is a key: exactly <see cref="HexLength"/> hexadecimal digits.</summary>
        public bool IsKeyValid => (bool)GetValue(IsKeyValidProperty);

        /// <summary>
        /// Hands out what is entered as a secret. It is a copy that the caller owns, disposes and no one else keeps.
        /// </summary>
        /// <returns>The entered characters.</returns>
        internal SecureString GetEntered()
        {
            if (!IsShown)
            {
                return HiddenBox.SecurePassword;
            }

            var secure = new SecureString();

            foreach (var character in ShownBox.Text)
            {
                secure.AppendChar(character);
            }

            secure.MakeReadOnly();

            return secure;
        }

        /// <summary>Empties both boxes and hides the key again.</summary>
        internal void Clear()
        {
            _switching = true;

            try
            {
                ShownBox.Clear();
                HiddenBox.Clear();
                IsShown = false;
            }
            finally
            {
                _switching = false;
            }

            Recompute();
        }

        private static void OnHexLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((KeyEntryBox)d).Recompute();

        private static void OnIsShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((KeyEntryBox)d).ApplyShown((bool)e.NewValue);

        // A tooltip opens in a popup of its own, which is not in the tree of this control: a binding that finds this control by walking
        // up from the tooltip finds nothing there, and the tooltip is empty. So the tooltip gets its text, not a binding.
        private static void OnShowKeyToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (KeyEntryBox)d;

            if (box.ShowCheck == null)
            {
                return;
            }

            var text = e.NewValue as string;

            box.ShowCheck.ToolTip = string.IsNullOrEmpty(text)
                ? null
                : new ToolTip { Style = (Style)box.FindResource("KeySlotToolTipStyle"), Content = text };
        }

        // The digits move from one box to the other, so that they are in one at a time.
        private void ApplyShown(bool shown)
        {
            if (!_switching)
            {
                _switching = true;

                try
                {
                    if (shown)
                    {
                        ShownBox.Text = HiddenBox.Password;
                        HiddenBox.Clear();
                    }
                    else
                    {
                        HiddenBox.Password = ShownBox.Text;
                        ShownBox.Clear();
                    }
                }
                finally
                {
                    _switching = false;
                }
            }

            HiddenBox.Visibility = shown ? Visibility.Collapsed : Visibility.Visible;
            ShownBox.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            Recompute();
            (shown ? (Control)ShownBox : HiddenBox).Focus();
        }

        private void HiddenBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_switching)
            {
                Recompute();
            }
        }

        private void ShownBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_switching)
            {
                Recompute();
            }
        }

        private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            EnterPressed?.Invoke(this, EventArgs.Empty);
        }

        // Judges what is entered, the way the key boxes of the task dialogs do: green on white for a key, black on red for anything
        // else, the empty box included. The entry is judged as a secret and never as a string.
        private void Recompute()
        {
            if (HiddenBox == null || ShownBox == null)
            {
                return;
            }

            bool valid;
            Span<byte> key = stackalloc byte[WindowsCredentialKeyProvider.MaxKeyBytes];

            try
            {
                using var entered = GetEntered();
                valid = KeyEntry.Read(entered, HexLength, key) > 0;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            SetValue(IsKeyValidPropertyKey, valid);

            foreach (var box in new Control[] { HiddenBox, ShownBox })
            {
                box.Foreground = valid ? Brushes.Green : Brushes.Black;
                box.Background = valid ? Brushes.White : Brushes.Red;
            }
        }
    }
}
