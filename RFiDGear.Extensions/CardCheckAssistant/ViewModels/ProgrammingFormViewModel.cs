using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>
    /// What the operator answers about the programming of a chip: the template used in the LSM, three yes-or-no
    /// questions, the text of the restriction and, for a Classic chip, the sectors used. It holds only that input and the
    /// rules about it; it knows no workflow and no services.
    /// <para>
    /// The page creates one form and keeps it. It is on a data context of its own in the window, so the page's
    /// "everything changed" notice, which comes with every picture of the reader, does not reach the boxes the operator is
    /// typing in. <see cref="Bind"/> brings the form in line with the workflow's picture without ever touching what the
    /// operator entered: it clears the form when the order is another (or none), fills it once when the programming
    /// begins, and says whether it can be edited.
    /// </para>
    /// </summary>
    internal sealed class ProgrammingFormViewModel : ObservableBase
    {
        private readonly LocalizedStrings _strings;
        private readonly Action _changed;
        private string? _key;
        private bool _seeded;
        private bool _editable;
        private IReadOnlyList<LsmTemplate> _templates = Array.Empty<LsmTemplate>();
        private LsmTemplate? _selectedTemplate;
        private string _restrictionText = string.Empty;
        private string _sectorsUsed = string.Empty;
        private IReadOnlyList<TextTemplate> _textBlocks = Array.Empty<TextTemplate>();
        private TextTemplate? _selectedTextBlock;

        /// <summary>Creates the form, empty.</summary>
        /// <param name="strings">The page's texts (the form's labels are looked up in them).</param>
        /// <param name="changed">Called when something the page shows about the form changed (the answers, the completeness).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public ProgrammingFormViewModel(LocalizedStrings strings, Action changed)
        {
            _strings = strings ?? throw new ArgumentNullException(nameof(strings));
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            Programming = new YesNoAnswer(OnAnswerChanged);
            FunctionTest = new YesNoAnswer(OnAnswerChanged);
            Restricted = new YesNoAnswer(() =>
            {
                OnPropertyChanged(nameof(IsRestricted));
                OnAnswerChanged();
            });
            InsertTextBlockCommand = new DelegateCommand(InsertTextBlock, () => IsEditable && IsRestricted && SelectedTextBlock != null);
        }

        /// <summary>Gets the page's texts, as a binding source: <c>{Binding Strings[Key]}</c>.</summary>
        public LocalizedStrings Strings => _strings;

        /// <summary>Gets a value indicating whether the form has been filled from a programmable chip's findings.</summary>
        public bool IsSeeded => _seeded;

        /// <summary>Gets a value indicating whether the operator can change the form: only while the chip is programmed. After that the answers are fixed.</summary>
        public bool IsEditable => _editable;

        /// <summary>Gets a value indicating whether the texts are shown read-only: locked, they can still be read, scrolled and copied.</summary>
        public bool IsReadOnly => !_editable;

        /// <summary>Gets the templates that fit the chip.</summary>
        public IReadOnlyList<LsmTemplate> Templates => _templates;

        /// <summary>Gets or sets the template used in the LSM. A null (what a list writes when its items are replaced) is ignored.</summary>
        public LsmTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (value != null && SetProperty(ref _selectedTemplate, value))
                {
                    OnPropertyChanged(nameof(IsClassic));
                    OnAnswerChanged();
                }
            }
        }

        /// <summary>Gets a value indicating whether the template is for a Classic chip, which needs the sectors used. Computed on read, so the form holds no library enum.</summary>
        public bool IsClassic => SelectedTemplate?.Family == TemplateFamily.Classic;

        /// <summary>Gets the question whether the programming succeeded.</summary>
        public YesNoAnswer Programming { get; }

        /// <summary>Gets the question whether the function test passed.</summary>
        public YesNoAnswer FunctionTest { get; }

        /// <summary>Gets the question whether the function is restricted.</summary>
        public YesNoAnswer Restricted { get; }

        /// <summary>Gets a value indicating whether the function was answered to be restricted.</summary>
        public bool IsRestricted => Restricted.Value == true;

        /// <summary>Gets or sets the text of the restriction. It is kept when the question is answered yes, no and yes again.</summary>
        public string RestrictionText
        {
            get => _restrictionText;
            set
            {
                if (SetProperty(ref _restrictionText, value ?? string.Empty))
                {
                    OnAnswerChanged();
                }
            }
        }

        /// <summary>Gets or sets the sectors used on a Classic chip, as the operator writes them.</summary>
        public string SectorsUsed
        {
            get => _sectorsUsed;
            set
            {
                if (SetProperty(ref _sectorsUsed, value ?? string.Empty))
                {
                    OnAnswerChanged();
                }
            }
        }

        /// <summary>Gets the text blocks the operator can put into the restriction text.</summary>
        public IReadOnlyList<TextTemplate> TextBlocks => _textBlocks;

        /// <summary>Gets a value indicating whether there are text blocks to choose from.</summary>
        public bool HasTextBlocks => _textBlocks.Count > 0;

        /// <summary>Gets or sets the text block that <see cref="InsertTextBlockCommand"/> puts into the restriction text. Choosing one changes nothing by itself.</summary>
        public TextTemplate? SelectedTextBlock
        {
            get => _selectedTextBlock;
            set
            {
                if (SetProperty(ref _selectedTextBlock, value))
                {
                    InsertTextBlockCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the command that appends the chosen text block to the restriction text.</summary>
        public DelegateCommand InsertTextBlockCommand { get; }

        /// <summary>Gets a value indicating whether everything that is needed is answered.</summary>
        public bool IsComplete => Missing().Count == 0;

        /// <summary>The names of what is still open, in the order of the form.</summary>
        /// <returns>The localized names; none when the form is complete.</returns>
        public IReadOnlyList<string> Missing()
        {
            var open = new List<string>(6);
            if (SelectedTemplate == null)
            {
                open.Add(_strings[Keys.GuidedFormTemplate]);
            }

            if (Programming.Value == null)
            {
                open.Add(_strings[Keys.GuidedQuestionProgramming]);
            }

            if (FunctionTest.Value == null)
            {
                open.Add(_strings[Keys.GuidedQuestionFunctionTest]);
            }

            if (Restricted.Value == null)
            {
                open.Add(_strings[Keys.GuidedQuestionRestricted]);
            }
            else if (IsRestricted && string.IsNullOrWhiteSpace(RestrictionText))
            {
                // A blank text would clear the notes of the first report.
                open.Add(_strings[Keys.GuidedFormRestriction]);
            }

            if (IsClassic && string.IsNullOrWhiteSpace(SectorsUsed))
            {
                open.Add(_strings[Keys.GuidedFormSectors]);
            }

            return open;
        }

        /// <summary>
        /// The questions that were answered "no" and are worth a second look before they go into the report: the
        /// programming and the function test. "Restricted: no" is the normal case and is never one of them.
        /// </summary>
        /// <returns>The localized names of the questions.</returns>
        public IReadOnlyList<string> NoAnswers()
        {
            var answered = new List<string>(2);
            if (Programming.Value == false)
            {
                answered.Add(_strings[Keys.GuidedQuestionProgramming]);
            }

            if (FunctionTest.Value == false)
            {
                answered.Add(_strings[Keys.GuidedQuestionFunctionTest]);
            }

            return answered;
        }

        /// <summary>The answers as the workflow takes them.</summary>
        /// <returns>
        /// The answers. The restriction text is given only when the function is restricted (<c>null</c> keeps the notes of the
        /// first report), and the sectors only for a Classic chip.
        /// </returns>
        public LsmAnswers ToAnswers() => new LsmAnswers
        {
            ProgrammingSucceeded = Programming.Value,
            FunctionTestPassed = FunctionTest.Value,
            HasRestrictions = Restricted.Value,
            RestrictionText = IsRestricted ? RestrictionText : null,
            SectorsUsed = IsClassic ? SectorsUsed.Trim() : string.Empty,
        };

        /// <summary>
        /// Brings the form in line with the workflow's picture. It never changes what the operator entered once the form is
        /// filled, and it announces only what changed.
        /// </summary>
        /// <param name="snapshot">The workflow's picture.</param>
        public void Bind(WorkflowSnapshot snapshot)
        {
            // The form belongs to the order. Back always passes through the list (no order), the end states keep the order.
            var id = snapshot.Order?.Id;
            if (!string.Equals(id, _key, StringComparison.Ordinal))
            {
                Clear();
                _key = id;
            }

            if (!_seeded && id != null && snapshot.State == WorkflowState.Programming && snapshot.Selection != null)
            {
                Seed(snapshot);
            }

            SetEditable(_seeded && snapshot.State == WorkflowState.Programming);
        }

        // The findings and the selection are final when the programming begins. The notes of the first report are the start
        // of the restriction text, because the text replaces them in the report.
        private void Seed(WorkflowSnapshot snapshot)
        {
            var selection = snapshot.Selection!;
            _templates = selection.Available;
            _selectedTemplate = selection.Default ?? _templates.FirstOrDefault();
            _restrictionText = snapshot.Findings?.Notes?.Trim() ?? string.Empty;
            _textBlocks = snapshot.TextBlocks;
            _selectedTextBlock = null;
            _seeded = true;
            OnPropertyChanged(string.Empty);
            InsertTextBlockCommand.RaiseCanExecuteChanged();
            _changed();
        }

        private void Clear()
        {
            var wasEmpty = !_seeded && Programming.Value == null && FunctionTest.Value == null && Restricted.Value == null;
            _seeded = false;
            _editable = false;
            _templates = Array.Empty<LsmTemplate>();
            _selectedTemplate = null;
            _restrictionText = string.Empty;
            _sectorsUsed = string.Empty;
            _textBlocks = Array.Empty<TextTemplate>();
            _selectedTextBlock = null;
            Programming.Reset();
            FunctionTest.Reset();
            Restricted.Reset();
            if (!wasEmpty)
            {
                OnPropertyChanged(string.Empty);
                InsertTextBlockCommand.RaiseCanExecuteChanged();
                _changed();
            }
        }

        private void SetEditable(bool editable)
        {
            if (SetProperty(ref _editable, editable, nameof(IsEditable)))
            {
                OnPropertyChanged(nameof(IsReadOnly));
                InsertTextBlockCommand.RaiseCanExecuteChanged();
                _changed();
            }
        }

        // The block comes after what is there, after a blank line; it is the text if there is none yet. Nothing is replaced.
        private void InsertTextBlock()
        {
            var content = SelectedTextBlock!.Content;
            RestrictionText = string.IsNullOrWhiteSpace(RestrictionText)
                ? content
                : RestrictionText.TrimEnd() + Environment.NewLine + Environment.NewLine + content;
        }

        private void OnAnswerChanged()
        {
            OnPropertyChanged(nameof(IsComplete));
            InsertTextBlockCommand.RaiseCanExecuteChanged();
            _changed();
        }

        /// <summary>One yes-or-no question. Unanswered (<c>null</c>) is not the same as "no".</summary>
        internal sealed class YesNoAnswer : ObservableBase
        {
            private readonly Action _answerChanged;
            private bool? _value;

            /// <summary>Creates the question, unanswered.</summary>
            /// <param name="answerChanged">Called when the answer changed.</param>
            public YesNoAnswer(Action answerChanged) => _answerChanged = answerChanged;

            /// <summary>Gets the answer; <c>null</c> while the question is not answered.</summary>
            public bool? Value => _value;

            /// <summary>
            /// Gets or sets whether the answer is yes. A radio button also writes <c>false</c> when the other button of its
            /// group is chosen; that is not an answer and is ignored.
            /// </summary>
            public bool IsYes
            {
                get => _value == true;
                set
                {
                    if (value)
                    {
                        Set(true);
                    }
                }
            }

            /// <summary>Gets or sets whether the answer is no; see <see cref="IsYes"/>.</summary>
            public bool IsNo
            {
                get => _value == false;
                set
                {
                    if (value)
                    {
                        Set(false);
                    }
                }
            }

            /// <summary>Takes the answer back, without telling the form (it clears itself as a whole).</summary>
            public void Reset()
            {
                if (_value != null)
                {
                    _value = null;
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(IsYes));
                    OnPropertyChanged(nameof(IsNo));
                }
            }

            private void Set(bool answer)
            {
                if (_value == answer)
                {
                    return;
                }

                _value = answer;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(IsYes));
                OnPropertyChanged(nameof(IsNo));
                _answerChanged();
            }
        }
    }
}
