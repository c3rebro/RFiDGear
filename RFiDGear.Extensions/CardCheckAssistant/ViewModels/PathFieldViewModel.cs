using System;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>
    /// One path the settings window edits: its text, the message about it (an error stops the save, a warning does not)
    /// and the button that picks it. The message is kept as a key, so it is written again when the language changes.
    /// </summary>
    internal sealed class PathFieldViewModel : ObservableBase
    {
        private readonly LocalizedStrings _strings;
        private readonly Action _edited;
        private string _value = string.Empty;
        private string? _messageKey;
        private bool _isError;

        /// <param name="strings">The page's texts.</param>
        /// <param name="edited">Called when the operator or the picker changes the text.</param>
        /// <param name="browse">Opens the picker for a path; it is given this field, to set the path that was picked.</param>
        /// <param name="canBrowse">Whether the picker can be opened now.</param>
        public PathFieldViewModel(LocalizedStrings strings, Action edited, Action<PathFieldViewModel> browse, Func<bool> canBrowse)
        {
            _strings = strings ?? throw new ArgumentNullException(nameof(strings));
            _edited = edited ?? throw new ArgumentNullException(nameof(edited));
            if (browse == null)
            {
                throw new ArgumentNullException(nameof(browse));
            }

            BrowseCommand = new DelegateCommand(() => browse(this), canBrowse);
        }

        /// <summary>Opens the picker.</summary>
        public DelegateCommand BrowseCommand { get; }

        /// <summary>The path as entered. Changing it clears the message about it.</summary>
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value ?? string.Empty))
                {
                    ClearMessage();
                    _edited();
                }
            }
        }

        /// <summary>What is wrong with the path, or <c>null</c>.</summary>
        public string? Message => _messageKey == null ? null : _strings[_messageKey];

        /// <summary>Whether <see cref="Message"/> has something to show.</summary>
        public bool HasMessage => _messageKey != null;

        /// <summary>Whether the message is an error, which stops the save.</summary>
        public bool IsError => _messageKey != null && _isError;

        /// <summary>Whether the message is only a warning, which does not stop the save.</summary>
        public bool IsWarning => _messageKey != null && !_isError;

        /// <summary>Shows a message about the path.</summary>
        /// <param name="key">One of <c>ExtensionStrings.Keys</c>.</param>
        /// <param name="isError">Whether it stops the save.</param>
        internal void SetMessage(string key, bool isError)
        {
            _messageKey = key;
            _isError = isError;
            RaiseMessageProperties();
        }

        /// <summary>Removes the message, if there is one.</summary>
        internal void ClearMessage()
        {
            if (_messageKey != null)
            {
                _messageKey = null;
                _isError = false;
                RaiseMessageProperties();
            }
        }

        /// <summary>Writes the message again, after the language changed.</summary>
        internal void RefreshText() => OnPropertyChanged(nameof(Message));

        private void RaiseMessageProperties()
        {
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(HasMessage));
            OnPropertyChanged(nameof(IsError));
            OnPropertyChanged(nameof(IsWarning));
        }
    }
}
