using System;
using System.Windows;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// The question of the key slot editor and of the key manager, asked with a message box: whether the key that is typed into
    /// the task may be thrown away for a reference. A key is entered, and what became of it is said, in the key manager itself.
    /// </summary>
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
        public bool ConfirmReplaceKey()
        {
            var text = ResourceLoader.GetResource("messageKeySlotReplaceKey");
            var caption = ResourceLoader.GetResource("captionKeySlot");
            var owner = _owner();

            var answer = owner != null
                ? MessageBox.Show(owner, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning)
                : MessageBox.Show(text, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            return answer == MessageBoxResult.Yes;
        }
    }
}
