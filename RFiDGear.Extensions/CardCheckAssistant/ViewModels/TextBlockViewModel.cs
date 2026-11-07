using System;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;
using RFiDGear.Extensions.CardCheckAssistant.Services;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>What is wrong with the name of a text block.</summary>
    internal enum TextBlockNameProblem
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>No name was entered.</summary>
        Blank,

        /// <summary>Another text block has the same name (case does not matter).</summary>
        Duplicate
    }

    /// <summary>One text block in the list of the settings window: a name and the text it inserts.</summary>
    internal sealed class TextBlockViewModel : ObservableBase
    {
        private readonly LocalizedStrings _strings;
        private readonly Action _edited;
        private string _name;
        private string _content;
        private TextBlockNameProblem _nameProblem;

        /// <param name="strings">The page's texts.</param>
        /// <param name="name">The name shown in the list.</param>
        /// <param name="content">The text that is inserted.</param>
        /// <param name="edited">Called when the name or the text changes.</param>
        public TextBlockViewModel(LocalizedStrings strings, string name, string content, Action edited)
        {
            _strings = strings ?? throw new ArgumentNullException(nameof(strings));
            _edited = edited ?? throw new ArgumentNullException(nameof(edited));
            _name = name ?? string.Empty;
            _content = content ?? string.Empty;
        }

        /// <summary>The name shown in the list.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value ?? string.Empty))
                {
                    _edited();
                }
            }
        }

        /// <summary>The text that is inserted; line breaks are kept.</summary>
        public string Content
        {
            get => _content;
            set
            {
                if (SetProperty(ref _content, value ?? string.Empty))
                {
                    _edited();
                }
            }
        }

        /// <summary>What is wrong with the name; set by the list, which can see the other names.</summary>
        public TextBlockNameProblem NameProblem
        {
            get => _nameProblem;
            internal set
            {
                if (SetProperty(ref _nameProblem, value))
                {
                    OnPropertyChanged(nameof(NameError));
                    OnPropertyChanged(nameof(HasNameError));
                }
            }
        }

        /// <summary>What is wrong with the name, or <c>null</c>.</summary>
        public string? NameError => _nameProblem switch
        {
            TextBlockNameProblem.Blank => _strings[Keys.TextBlockNameBlank],
            TextBlockNameProblem.Duplicate => _strings[Keys.TextBlockNameDuplicate],
            _ => null
        };

        /// <summary>Whether <see cref="NameError"/> has something to show.</summary>
        public bool HasNameError => _nameProblem != TextBlockNameProblem.None;

        /// <summary>Writes the error again, after the language changed.</summary>
        internal void RefreshText() => OnPropertyChanged(nameof(NameError));

        /// <summary>The block as the settings keep it: the name without the blanks around it, the text as typed.</summary>
        /// <returns>The data to save.</returns>
        internal TextBlockData ToData() => new TextBlockData(_name.Trim(), _content);
    }
}
