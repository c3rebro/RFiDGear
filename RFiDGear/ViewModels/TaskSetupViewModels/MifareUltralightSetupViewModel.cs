/* Created by SharpDevelop.
 * Date: 10/11/2017
 * Time: 22:15
 *
 */

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Infrastructure.Tasks.Interfaces;
using Serilog;
using RFiDGear.Infrastructure.ReaderProviders;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;
using RFiDGear.UI.MVVMDialogs.ViewModels.Interfaces;
using RFiDGear.Infrastructure.Ultralight;
using RFiDGear.Contracts;
using RFiDGear.Services.SecureKeys;
using RFiDGear.Services.TaskExecution;
using System.Globalization;

namespace RFiDGear.ViewModel.TaskSetupViewModels
{
    /// <summary>
    /// The setup of a task for MIFARE Ultralight (and the chips that share its page commands). It is a task kind of the
    /// application like the others: the run loop runs it through <see cref="CommandDelegator"/>, and the project stores what
    /// the user chose and nothing a run or the open dialog leaves behind.
    /// </summary>
    public class MifareUltralightSetupViewModel : ObservableObject, IUserDialogViewModel, IGenericTask, ITaskOutcomeDetails, IKeySlotHost, IKeyMigrationHost
    {
        #region Fields
        private readonly object editedTaskReference; // Tracks the original task instance during edit mode.

        private SettingsReaderWriter settings = new SettingsReaderWriter();

        #endregion fields

        #region Constructors

        /// <summary>
        ///
        /// </summary>
        public MifareUltralightSetupViewModel()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="_selectedSetupViewModel"></param>
        /// <param name="_dialogs"></param>
        public MifareUltralightSetupViewModel(object _selectedSetupViewModel, ObservableCollection<IDialogViewModel> _dialogs)
        {
            try
            {
                MefHelper.Instance.EnsureCompose();
                MefHelper.Instance.Container?.ComposeParts(this); //Load Plugins

                if (_selectedSetupViewModel is MifareUltralightSetupViewModel)
                {
                    editedTaskReference = _selectedSetupViewModel;
                    var properties = typeof(MifareUltralightSetupViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    foreach (var p in properties)
                    {
                        // If not writable then cannot null it; if not readable then cannot check it's value
                        if (!p.CanWrite || !p.CanRead) { continue; }

                        var mget = p.GetGetMethod(false);
                        var mset = p.GetSetMethod(false);

                        // Get and set methods have to be public
                        if (mget == null) { continue; }
                        if (mset == null) { continue; }

                        // What this dialog composes for itself is no data of the task. The plugins of the edited task were never composed
                        // (it came out of a project file), and copying their absence would throw in the setter, stop the copy at this
                        // property and leave every property behind it at its default: the dialog would open empty.
                        if (p.Name == nameof(Items) || p.Name == nameof(HasPlugins) || p.Name == nameof(SelectedPlugin)) { continue; }

                        try
                        {
                            p.SetValue(this, p.GetValue(_selectedSetupViewModel));
                        }
                        catch (Exception e) when (!(e is OperationCanceledException))
                        {
                            // The name of the property and the type of the exception, nothing of the value.
                            Log.ForContext<MifareUltralightSetupViewModel>().Error("A property of the edited task could not be copied ({Property}, {ExceptionType})", p.Name, (e.InnerException ?? e).GetType().FullName);
                        }
                    }
                }

                else
                {
                    editedTaskReference = null;
                    CurrentTaskIndex = "0";
                    SelectedTaskDescription = "Enter a Description";
                }

                HasPlugins = items != null ? items.Any() : false;

                if (HasPlugins)
                {
                    SelectedPlugin = Items.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                Log.ForContext<MifareUltralightSetupViewModel>().Error(e, "Mifare Ultralight setup operation failed.");
            }

        }

        #endregion

        #region DataExplorer

        /// <summary>
        /// Gets or sets whether the pages of the dialog take input: they do for a read and for a write.
        /// </summary>
        [XmlIgnore]
        public bool IsDataExplorerEditTabEnabled
        {
            get => isDataExplorerEditTabEnabled;
            set
            {
                isDataExplorerEditTabEnabled = value;
                OnPropertyChanged(nameof(IsDataExplorerEditTabEnabled));
            }
        }
        private bool isDataExplorerEditTabEnabled;

        #endregion DataExplorer

        #region The grid of pages

        // The biggest chip of the family, an NTAG216, has 231 pages: a grid never has more rows than that.
        private static readonly int MostPagesOfAnyChip = Enum.GetValues(typeof(UltralightVariant)).Cast<UltralightVariant>().Max(variant => UltralightMemoryMap.For(variant).PageCount);

        // Set while the data of the task is made from the rows, so that the rows are not made from the data again: a row that is typed
        // into and not yet whole would otherwise be cut up differently from the way the person sees it.
        private bool dataIsMadeFromTheRows;

        /// <summary>
        /// Gets the rows of the grid of pages: for a write one per page of its data, which the person types into and which change the
        /// data of the task; for a read one per page that it asks for, showing what the last read gave. The rows are a view of the task
        /// and no part of the project.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<UltralightPageRow> PageRows { get; } = new ObservableCollection<UltralightPageRow>();

        /// <summary>Gets whether the task reads pages.</summary>
        [XmlIgnore]
        public bool IsReadTask => SelectedTaskType == TaskType_MifareUltralightTask.ReadData;

        /// <summary>Gets whether the task writes pages.</summary>
        [XmlIgnore]
        public bool IsWriteTask => SelectedTaskType == TaskType_MifareUltralightTask.WriteData;

        /// <summary>Gets whether the task authenticates with the key of an Ultralight C: the box of that key is on.</summary>
        [XmlIgnore]
        public bool UsesUltralightCKey => Authentication == UltralightAuth.UltralightC;

        /// <summary>Gets whether the task authenticates with the password of an EV1 or an NTAG: the boxes of the password and the PACK are on.</summary>
        [XmlIgnore]
        public bool UsesPassword => Authentication == UltralightAuth.Password;

        private IRelayCommand addPageCommand;

        /// <summary>Gets the command that adds an empty page at the end of the data of a write. It is off for a read and when the chip has no more pages.</summary>
        public IRelayCommand AddPageCommand => addPageCommand ??= new RelayCommand(AddPage, CanAddPage);

        private IRelayCommand<UltralightPageRow> removePageCommand;

        /// <summary>Gets the command that removes a page of a write; its parameter is the row.</summary>
        public IRelayCommand<UltralightPageRow> RemovePageCommand => removePageCommand ??= new RelayCommand<UltralightPageRow>(RemovePage);

        // The pages that the grid can have: the chip the task names has as many as its layout says, a task that names none can have the
        // pages of the biggest chip. The rows start at the first page of the task.
        private int MostRows()
        {
            var pages = ExpectedChip.TryGetVariant(out var variant) ? UltralightMemoryMap.For(variant).PageCount : MostPagesOfAnyChip;

            return Math.Max(0, pages - Math.Max(StartPage, 0));
        }

        private bool CanAddPage() => IsWriteTask && PageRows.Count < MostRows();

        private void AddPage()
        {
            if (!CanAddPage())
            {
                return;
            }

            AddRow(string.Empty, readOnly: false);
            Renumber();
            MakeTheDataFromTheRows();
        }

        private void RemovePage(UltralightPageRow row)
        {
            if (row == null || !IsWriteTask || !PageRows.Contains(row))
            {
                return;
            }

            row.PropertyChanged -= OnRowChanged;
            PageRows.Remove(row);
            Renumber();
            MakeTheDataFromTheRows();
        }

        private void AddRow(string hex, bool readOnly)
        {
            var row = new UltralightPageRow(hex, readOnly);
            row.PropertyChanged += OnRowChanged;
            PageRows.Add(row);
        }

        private void OnRowChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UltralightPageRow.Hex) && IsWriteTask)
            {
                MakeTheDataFromTheRows();
            }
        }

        // The data of a write is what the rows hold, one after the other, as long as every row is a whole page. With a row that is
        // not (two rows A1B2 and C3D4, or an empty row before a full one) the plain digits could be one whole page that the grid does
        // not show, or the data of a later row one page too early: the rows are kept exactly as they are, each after a mark, and
        // that text is no data that a run takes (it is not hexadecimal), so the write is refused. The grid reads it back as it was.
        private void MakeTheDataFromTheRows()
        {
            dataIsMadeFromTheRows = true;

            try
            {
                var digits = PageRows.Select(row => row.Hex).ToList();

                WriteDataHex = digits.All(UltralightPageRow.IsWholePage)
                    ? string.Concat(digits)
                    : UltralightPageRow.RowMark + string.Join(UltralightPageRow.RowMark.ToString(), digits);
            }
            finally
            {
                dataIsMadeFromTheRows = false;
            }

            addPageCommand?.NotifyCanExecuteChanged();
        }

        // The rows are made again from what the task holds: from its data for a write, and for a read from the pages it asks for and the
        // bytes of the last read.
        private void RebuildPageRows()
        {
            foreach (var row in PageRows)
            {
                row.PropertyChanged -= OnRowChanged;
            }

            PageRows.Clear();

            if (IsWriteTask && WriteDataHex.Length > 0 && WriteDataHex[0] == UltralightPageRow.RowMark)
            {
                // Rows that were kept as they were shown (see MakeTheDataFromTheRows).
                foreach (var digits in WriteDataHex.Substring(1).Split(UltralightPageRow.RowMark))
                {
                    AddRow(digits, readOnly: false);
                }
            }
            else if (IsWriteTask)
            {
                for (var offset = 0; offset < WriteDataHex.Length; offset += 8)
                {
                    AddRow(WriteDataHex.Substring(offset, Math.Min(8, WriteDataHex.Length - offset)), readOnly: false);
                }
            }
            else if (IsReadTask)
            {
                var count = Math.Min(Math.Max(PageCount, 0), MostPagesOfAnyChip);

                for (var i = 0; i < count; i++)
                {
                    AddRow(ShownOfTheLastRead(unchecked(StartPage + i)), readOnly: true);
                }
            }

            Renumber();
            addPageCommand?.NotifyCanExecuteChanged();
        }

        private void Renumber()
        {
            for (var i = 0; i < PageRows.Count; i++)
            {
                // A first page near the end of the numbers must not stop the grid: the application is built with overflow checks.
                PageRows[i].PageNumber = unchecked(StartPage + i);
            }
        }

        // The bytes that the last read gave of a page, as digits; nothing for a page that was not read (the bytes belong to the pages
        // they were read from, not to the place in the grid).
        private string ShownOfTheLastRead(int page)
        {
            // In 64 bits: the pages are those of any number a task can hold, and the difference of two of them does not fit 32.
            var offset = ((long)page - LastReadStartPage) * UltralightMemoryMap.PageSize;

            if (LastReadData == null || offset < 0 || offset + UltralightMemoryMap.PageSize > LastReadData.Length)
            {
                return string.Empty;
            }

            return Convert.ToHexString(LastReadData, (int)offset, UltralightMemoryMap.PageSize);
        }

        #endregion The grid of pages

        #region Plugins

        /// <summary>
        /// True if Count of Imported Views is > 0; See constructor
        /// </summary>
        [XmlIgnore]
        public bool HasPlugins
        {
            get => hasPlugins;
            set
            {
                hasPlugins = value;
                OnPropertyChanged(nameof(HasPlugins));
            }
        }
        private bool hasPlugins;

        /// <summary>
        /// Selected Plugin in ComboBox
        /// </summary>
        [XmlIgnore]
        public object SelectedPlugin
        {
            get => selectedPlugin;
            set
            {
                selectedPlugin = value;
                OnPropertyChanged(nameof(SelectedPlugin));
            }
        }
        private object selectedPlugin;

        /// <summary>
        /// Imported Views by URI
        /// </summary>
        [XmlIgnore]
        [ImportMany()]
        public Lazy<IUIExtension, IUIExtensionDetails>[] Items
        {
            get => items;

            set
            {
                items = (from g in value
                         where g.Metadata.HostPlacement != HostPlacement.Application
                         orderby g.Metadata.SortOrder, g.Metadata.Name
                         select g).ToArray();

                OnPropertyChanged(nameof(Items));
            }
        }
        private Lazy<IUIExtension, IUIExtensionDetails>[] items;
        #endregion

        #region Dependency Properties

        /// <summary>
        /// The Indexnumber of the ExecuteCondition Task As String
        /// </summary>
        public string SelectedExecuteConditionTaskIndex
        {
            get => selectedExecuteConditionTaskIndex;

            set
            {
                selectedExecuteConditionTaskIndex = value;
                IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(value, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
                int.TryParse(value, out selectedExecuteConditionTaskIndexAsInt);
                OnPropertyChanged(nameof(SelectedExecuteConditionTaskIndex));
            }
        }
        private string selectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedExecuteConditionTaskIndex
        {
            get => isValidSelectedExecuteConditionTaskIndex;
            set
            {
                isValidSelectedExecuteConditionTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedExecuteConditionTaskIndex));
            }
        }
        private bool? isValidSelectedExecuteConditionTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public int SelectedExecuteConditionTaskIndexAsInt => selectedExecuteConditionTaskIndexAsInt;
        private int selectedExecuteConditionTaskIndexAsInt;

        /// <summary>
        /// 
        /// </summary>
        public ERROR SelectedExecuteConditionErrorLevel
        {
            get => selectedExecuteConditionErrorLevel;

            set
            {
                selectedExecuteConditionErrorLevel = value;
                RevalidateSelectedExecuteConditionTaskIndex();
                OnPropertyChanged(nameof(SelectedExecuteConditionErrorLevel));
            }
        }
        private ERROR selectedExecuteConditionErrorLevel;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public ERROR CurrentTaskErrorLevel { get; set; }

        /// <summary>
        /// Where the run loop is with this task in the current run. Reset at the start of a run; not part of the project.
        /// </summary>
        [XmlIgnore]
        public TaskExecutionState ExecutionState { get; set; } = TaskExecutionState.NotStarted;

        [XmlIgnore]
        public ObservableCollection<TaskAttemptResult> AttemptResults { get; } = new ObservableCollection<TaskAttemptResult>();

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsTaskCompletedSuccessfully
        {
            get => isTaskCompletedSuccessfully;
            set
            {
                if (isTaskCompletedSuccessfully == value)
                {
                    return;
                }

                isTaskCompletedSuccessfully = value;
                UiDispatcher.InvokeIfRequired(() => OnPropertyChanged(nameof(IsTaskCompletedSuccessfully)));
            }
        }
        private bool? isTaskCompletedSuccessfully;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public string StatusText
        {
            get => statusText;
            set
            {
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
        private string statusText;

        /// <summary>
        ///
        /// </summary>
        public string CurrentTaskIndex
        {
            get =>
                currentTaskIndex;
            set
            {
                currentTaskIndex = value;
                IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(value, AvailableTasks, editedTaskReference ?? this, out _);
                int.TryParse(value, out selectedTaskIndexAsInt);
            }
        }
        private string currentTaskIndex;

        /// <summary>
        ///
        /// </summary>
        public int SelectedTaskIndexAsInt => selectedTaskIndexAsInt;
        private int selectedTaskIndexAsInt;

        /// <summary>
        ///
        /// </summary>
        [XmlIgnore]
        public bool? IsValidSelectedTaskIndex
        {
            get => isValidSelectedTaskIndex;
            set
            {
                isValidSelectedTaskIndex = value;
                OnPropertyChanged(nameof(IsValidSelectedTaskIndex));
            }
        }
        private bool? isValidSelectedTaskIndex;

        /// <summary>
        /// The collection of available tasks for validation.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<object> AvailableTasks
        {
            get => availableTasks;
            set
            {
                availableTasks = value;
                RevalidateSelectedTaskIndex();
                RevalidateSelectedExecuteConditionTaskIndex();
            }
        }
        private ObservableCollection<object> availableTasks;

        private void RevalidateSelectedTaskIndex()
        {
            IsValidSelectedTaskIndex = TaskIndexValidation.TryValidateTaskIndex(CurrentTaskIndex, AvailableTasks, editedTaskReference ?? this, out _);
        }

        private void RevalidateSelectedExecuteConditionTaskIndex()
        {
            IsValidSelectedExecuteConditionTaskIndex = TaskIndexValidation.TryValidateExecuteConditionIndex(SelectedExecuteConditionTaskIndex, SelectedExecuteConditionErrorLevel, AvailableTasks, out _);
        }

        /// <summary>
        ///
        /// </summary>
        public TaskType_MifareUltralightTask SelectedTaskType
        {
            get => selectedTaskType;
            set
            {
                selectedTaskType = value;
                switch (selectedTaskType)
                {
                    case TaskType_MifareUltralightTask.ReadData:
                    case TaskType_MifareUltralightTask.WriteData:
                        IsDataExplorerEditTabEnabled = true;
                        break;

                    default:
                        IsDataExplorerEditTabEnabled = false;
                        break;
                }
                OnPropertyChanged(nameof(SelectedTaskType));
                OnPropertyChanged(nameof(IsReadTask));
                OnPropertyChanged(nameof(IsWriteTask));
                RebuildPageRows();
                PageInputsChanged();
            }
        }
        private TaskType_MifareUltralightTask selectedTaskType;

        #region What the task does

        /// <summary>
        /// Gets or sets the first page of a read or a write. The user area of every chip of the family starts at page 4: pages 0 to 3 hold
        /// the UID, the lock bytes and the one-time-programmable bytes.
        /// </summary>
        public int StartPage
        {
            get => startPage;
            set
            {
                startPage = value;
                OnPropertyChanged();

                if (!startPageIsFromText)
                {
                    // The program set the page: what the box holds is that page, and no mistake.
                    startPageText = null;
                    startPageIsValid = true;
                    OnPropertyChanged(nameof(StartPageText));
                    OnPropertyChanged(nameof(IsStartPageValid));
                    PageInputsChanged();
                }

                // The pages of a write keep their data and take new numbers, the pages of a read show what was read of their number.
                if (IsWriteTask)
                {
                    Renumber();
                    addPageCommand?.NotifyCanExecuteChanged();
                }
                else
                {
                    RebuildPageRows();
                }
            }
        }
        private int startPage = UltralightMemoryMap.FirstUserPage;

        // What the boxes of the first page and of the number of pages hold, as it is typed. WPF leaves the number of the task as it was
        // when a box holds no number, and a button that runs the task would then act on a page that the box no longer shows: the text
        // is kept here, the box says when it is no number, and nothing runs while it is not.
        private string startPageText;
        private bool startPageIsValid = true;
        private bool startPageIsFromText;
        private string pageCountText;
        private bool pageCountIsValid = true;
        private bool pageCountIsFromText;

        /// <summary>
        /// Gets or sets the first page as it is typed. While it is no whole number the task keeps the page it had, <see cref="IsStartPageValid"/>
        /// is <see langword="false"/>, and nothing is run on that page. Not part of the project.
        /// </summary>
        [XmlIgnore]
        public string StartPageText
        {
            get => startPageText ?? startPage.ToString(CultureInfo.InvariantCulture);
            set
            {
                startPageText = value ?? string.Empty;
                // A page of a chip: no chip of the family has more than MostPagesOfAnyChip pages, and none has a page below 0.
                startPageIsValid = int.TryParse(startPageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) && page >= 0 && page < MostPagesOfAnyChip;

                if (startPageIsValid)
                {
                    startPageIsFromText = true;

                    try
                    {
                        StartPage = page;
                    }
                    finally
                    {
                        startPageIsFromText = false;
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStartPageValid));
                PageInputsChanged();
            }
        }

        /// <summary>Gets whether the box of the first page holds a page of a chip (a whole number from 0 up to the pages of the biggest chip). Not part of the project.</summary>
        [XmlIgnore]
        public bool IsStartPageValid => startPageIsValid;

        /// <summary>
        /// Gets or sets the number of pages of a read as it is typed. While it is no whole number the task keeps the number it had,
        /// <see cref="IsPageCountValid"/> is <see langword="false"/>, and a read does not run. Not part of the project.
        /// </summary>
        [XmlIgnore]
        public string PageCountText
        {
            get => pageCountText ?? pageCount.ToString(CultureInfo.InvariantCulture);
            set
            {
                pageCountText = value ?? string.Empty;
                pageCountIsValid = int.TryParse(pageCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0 && count <= MostPagesOfAnyChip;

                if (pageCountIsValid)
                {
                    pageCountIsFromText = true;

                    try
                    {
                        PageCount = count;
                    }
                    finally
                    {
                        pageCountIsFromText = false;
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPageCountValid));
                PageInputsChanged();
            }
        }

        /// <summary>Gets whether the box of the number of pages holds a number of pages that a chip can have. Not part of the project.</summary>
        [XmlIgnore]
        public bool IsPageCountValid => pageCountIsValid;

        /// <summary>
        /// Gets whether the boxes that the task needs hold numbers: the first page, and for a read the number of pages. While they do
        /// not, the buttons that run the task and the OK button are off and a run is refused. Not part of the project.
        /// </summary>
        [XmlIgnore]
        public bool ArePageInputsValid => startPageIsValid && (!IsReadTask || pageCountIsValid);

        private void PageInputsChanged()
        {
            OnPropertyChanged(nameof(ArePageInputsValid));
            NotifyTheButtons();
        }

        /// <summary>Gets or sets the number of pages a read returns. A write has as many pages as its data has.</summary>
        public int PageCount
        {
            get => pageCount;
            set
            {
                pageCount = value;
                OnPropertyChanged();

                if (!pageCountIsFromText)
                {
                    pageCountText = null;
                    pageCountIsValid = true;
                    OnPropertyChanged(nameof(PageCountText));
                    OnPropertyChanged(nameof(IsPageCountValid));
                    PageInputsChanged();
                }

                if (IsReadTask)
                {
                    RebuildPageRows();
                }
            }
        }
        private int pageCount = 1;

        /// <summary>
        /// Gets or sets the data of a write, as hexadecimal digits without separators, 8 per page. White space is dropped and the
        /// digits are written in upper case; what is not a digit is kept, and a write with such data does not reach the card. The grid
        /// of the dialog writes a vertical bar in front of the rows, and between them, while one of them is not a whole page, so that
        /// rows that are shown apart are never run together into a page: such text is not data, and no write takes it.
        /// </summary>
        public string WriteDataHex
        {
            get => writeDataHex;
            set
            {
                writeDataHex = NormalizeHex(value);
                OnPropertyChanged();

                if (!dataIsMadeFromTheRows && IsWriteTask)
                {
                    RebuildPageRows();
                }
            }
        }
        private string writeDataHex = string.Empty;

        /// <summary>
        /// Gets or sets the chip the task expects. <see cref="UltralightExpectation.Auto"/> takes the chip that is found. A call that
        /// authenticates or writes a protected page has to name the chip: the key of an Ultralight C is not the password of an NTAG,
        /// and the protected pages differ per chip.
        /// </summary>
        public UltralightExpectation ExpectedChip
        {
            get => expectedChip;
            set
            {
                expectedChip = value;
                OnPropertyChanged();
                addPageCommand?.NotifyCanExecuteChanged();
            }
        }
        private UltralightExpectation expectedChip = UltralightExpectation.Auto;

        /// <summary>Gets or sets how the task authenticates before it reads or writes: not at all, with the key of an Ultralight C, or with the password and PACK of an EV1 or an NTAG.</summary>
        public UltralightAuth Authentication
        {
            get => authentication;
            set
            {
                authentication = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsesUltralightCKey));
                OnPropertyChanged(nameof(UsesPassword));
            }
        }
        private UltralightAuth authentication = UltralightAuth.None;

        /// <summary>
        /// Gets or sets whether the task may write the pages that are not user memory: the first four pages and the lock, configuration,
        /// password and key pages behind the user area. A wrong write there can lock the chip or its password for good.
        /// </summary>
        public bool AllowProtectedPages
        {
            get => allowProtectedPages;
            set
            {
                allowProtectedPages = value;
                OnPropertyChanged();
            }
        }
        private bool allowProtectedPages;

        // Hexadecimal text as the project keeps it: no white space, upper case. What is not a digit is left for the call to refuse.
        private static string NormalizeHex(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

        #endregion What the task does

        #region Keys

        /// <summary>Gets or sets the key of an Ultralight C, 32 hexadecimal digits (16 bytes), when it is typed into the task and not bound to a provider.</summary>
        public string UltralightCKey
        {
            get => ultralightCKey;
            set
            {
                ultralightCKey = NormalizeHex(value);
                OnPropertyChanged();
            }
        }
        private string ultralightCKey = string.Empty;

        /// <summary>Gets or sets the password of an Ultralight EV1 or an NTAG, 8 hexadecimal digits (4 bytes), when it is typed into the task and not bound to a provider.</summary>
        public string UltralightPassword
        {
            get => ultralightPassword;
            set
            {
                ultralightPassword = NormalizeHex(value);
                OnPropertyChanged();
            }
        }
        private string ultralightPassword = string.Empty;

        /// <summary>Gets or sets the PACK of an Ultralight EV1 or an NTAG, 4 hexadecimal digits (2 bytes), when it is typed into the task and not bound to a provider.</summary>
        public string UltralightPack
        {
            get => ultralightPack;
            set
            {
                ultralightPack = NormalizeHex(value);
                OnPropertyChanged();
            }
        }
        private string ultralightPack = string.Empty;

        private KeyBindingEntry[] keyBindings = Array.Empty<KeyBindingEntry>();

        /// <summary>
        /// Gets or sets the key slots of this task whose key comes from a secure key provider instead of from the project. A slot that
        /// has an entry here never uses its embedded key. The list is replaced as a whole and both accessors copy it: the dialog that
        /// edits a task copies every public property by reference, and a shared list would let a cancelled edit change the task.
        /// </summary>
        public KeyBindingEntry[] KeyBindings
        {
            get => KeyBindingList.Copy(keyBindings);
            set
            {
                keyBindings = KeyBindingList.Copy(value);
                OnPropertyChanged();
            }
        }

        /// <summary>Tells the serializer to write <see cref="KeyBindings"/> only when there are any, so that a task without bindings is saved as it always was.</summary>
        /// <returns><see langword="true"/> when the task has at least one key binding.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool ShouldSerializeKeyBindings() => keyBindings.Length > 0;

        /// <summary>Returns whether a slot has a key binding, usable or not; see <see cref="SecureKeySlots"/> for the slot ids.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns><see langword="true"/> when the slot has an entry in <see cref="KeyBindings"/>.</returns>
        public bool IsKeyBound(string slotId) => KeyBindingList.IsBound(keyBindings, slotId);

        /// <summary>Returns a copy of the binding of a slot.</summary>
        /// <param name="slotId">The slot id.</param>
        /// <returns>The binding, or <see langword="null"/> when the slot has none.</returns>
        public KeyBindingEntry GetKeyBinding(string slotId) => KeyBindingList.Find(keyBindings, slotId)?.Clone();

        // The embedded key of a slot that is bound is not written, so a project that was moved to a provider does not keep the old key
        // beside the reference. The properties stay readable and writable, to load older projects.

        /// <summary>Tells the serializer not to write the embedded key of an Ultralight C when its slot is bound.</summary>
        /// <returns><see langword="true"/> when the slot is not bound.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool ShouldSerializeUltralightCKey() => !IsKeyBound(SecureKeySlots.UltralightCKey);

        /// <summary>Tells the serializer not to write the embedded password when its slot is bound.</summary>
        /// <returns><see langword="true"/> when the slot is not bound.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool ShouldSerializeUltralightPassword() => !IsKeyBound(SecureKeySlots.UltralightPassword);

        /// <summary>Tells the serializer not to write the embedded PACK when its slot is bound.</summary>
        /// <returns><see langword="true"/> when the slot is not bound.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool ShouldSerializeUltralightPack() => !IsKeyBound(SecureKeySlots.UltralightPack);

        // What the key slot editors of the dialog ask of the task (see IKeySlotHost). IsKeyBound and GetKeyBinding are above.

        /// <inheritdoc />
        public bool HasEmbeddedKey(string slotId) => !string.IsNullOrEmpty(EmbeddedKeyOf(slotId));

        /// <inheritdoc />
        public int ExpectedKeyHexLength(string slotId) => slotId switch
        {
            SecureKeySlots.UltralightCKey => 32,
            SecureKeySlots.UltralightPassword => 8,
            SecureKeySlots.UltralightPack => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(slotId), "Not a key slot of a MIFARE Ultralight task."),
        };

        /// <inheritdoc />
        public void BindKeySlot(string slotId, string providerId, string reference)
        {
            ArgumentNullException.ThrowIfNull(providerId);
            ArgumentNullException.ThrowIfNull(reference);

            if (!SecureKeySlots.Ultralight.Contains(slotId))
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), "Not a key slot of a MIFARE Ultralight task.");
            }

            // The binding first and the key second: a slot has one or the other, and a failure in between leaves the binding.
            KeyBindings = KeyBindingList.With(keyBindings, new KeyBindingEntry { Slot = slotId, ProviderId = providerId, Reference = reference });
            SetEmbeddedKey(slotId, string.Empty);
        }

        /// <inheritdoc />
        public void UnbindKeySlot(string slotId)
        {
            // A task that was loaded can hold a key next to its binding (a file that was edited by hand): it is not reactivated by
            // "Use key", the person types the key again. It goes while the slot is still bound, so that no save in between can write it.
            if (SecureKeySlots.Ultralight.Contains(slotId) && IsKeyBound(slotId))
            {
                SetEmbeddedKey(slotId, string.Empty);
            }

            KeyBindings = KeyBindingList.Without(keyBindings, slotId);
        }

        /// <inheritdoc />
        public IReadOnlyList<string> EmbeddedKeySlots() =>
            SecureKeySlots.Ultralight.Where(slot => HasEmbeddedKey(slot) && IsWrittenToProject(slot)).ToList();

        /// <inheritdoc />
        public string GetEmbeddedKey(string slotId) => EmbeddedKeyOf(slotId) ?? string.Empty;

        private string EmbeddedKeyOf(string slotId) => slotId switch
        {
            SecureKeySlots.UltralightCKey => UltralightCKey,
            SecureKeySlots.UltralightPassword => UltralightPassword,
            SecureKeySlots.UltralightPack => UltralightPack,
            _ => throw new ArgumentOutOfRangeException(nameof(slotId), "Not a key slot of a MIFARE Ultralight task."),
        };

        // The rules the serializer asks, so that this is the list of what the file holds.
        private bool IsWrittenToProject(string slotId) => slotId switch
        {
            SecureKeySlots.UltralightCKey => ShouldSerializeUltralightCKey(),
            SecureKeySlots.UltralightPassword => ShouldSerializeUltralightPassword(),
            SecureKeySlots.UltralightPack => ShouldSerializeUltralightPack(),
            _ => throw new ArgumentOutOfRangeException(nameof(slotId), "Not a key slot of a MIFARE Ultralight task."),
        };

        private void SetEmbeddedKey(string slotId, string value)
        {
            switch (slotId)
            {
                case SecureKeySlots.UltralightCKey:
                    UltralightCKey = value;
                    break;
                case SecureKeySlots.UltralightPassword:
                    UltralightPassword = value;
                    break;
                case SecureKeySlots.UltralightPack:
                    UltralightPack = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slotId), "Not a key slot of a MIFARE Ultralight task.");
            }
        }

        #endregion Keys

        /// <summary>
        ///
        /// </summary>
        public string SelectedTaskDescription
        {
            get => selectedAccessBitsTaskDescription;
            set
            {
                selectedAccessBitsTaskDescription = value;
                OnPropertyChanged(nameof(SelectedTaskDescription));
            }
        }
        private string selectedAccessBitsTaskDescription;

        /// <summary>
        /// 
        /// </summary>
        [XmlIgnore]
        public SettingsReaderWriter Settings => settings;

        #endregion General Properties

        #region Commands

        /// <summary>
        /// Runs the command of a task type; the run loop awaits it, once per task and attempt.
        /// </summary>
        public IAsyncRelayCommand CommandDelegator => new AsyncRelayCommand<TaskType_MifareUltralightTask>(OnNewCommandDelegatorCall);
        private async Task OnNewCommandDelegatorCall(TaskType_MifareUltralightTask taskType)
        {
            switch (taskType)
            {
                case TaskType_MifareUltralightTask.ReadData:
                    await RunPageOperationAsync(write: false);
                    break;

                case TaskType_MifareUltralightTask.WriteData:
                    await RunPageOperationAsync(write: true);
                    break;

                default:
                    break;
            }
        }

        public IAsyncRelayCommand SaveSettings => new AsyncRelayCommand(OnNewSaveSettingsCommand);
        private async Task OnNewSaveSettingsCommand()
        {
            SettingsReaderWriter settings = new SettingsReaderWriter();
            await settings.SaveSettings();
        }

        private IAsyncRelayCommand readDataCommand;

        /// <summary>
        /// Reads the pages of the task from the chip on the reader, for the button of the dialog: the pages the dialog shows are the ones
        /// the task holds. It is off while an operation runs, and is the same command each time so that the button is told when that
        /// changes. The run loop uses <see cref="CommandDelegator"/>.
        /// </summary>
        public IAsyncRelayCommand ReadDataCommand => readDataCommand ??= new AsyncRelayCommand(() => RunPageOperationAsync(write: false), () => !IsRunning && ArePageInputsValid);

        private IAsyncRelayCommand writeDataCommand;

        /// <summary>
        /// Writes the data of the task to the chip on the reader, for the button of the dialog. Off while an operation runs, like
        /// <see cref="ReadDataCommand"/>.
        /// </summary>
        public IAsyncRelayCommand WriteDataCommand => writeDataCommand ??= new AsyncRelayCommand(() => RunPageOperationAsync(write: true), () => !IsRunning && ArePageInputsValid);

        // The buttons are told when an operation starts and when it ends. They are told on the thread of the window: the run loop
        // can run the task on another.
        private void NotifyTheButtons() =>
            UiDispatcher.InvokeIfRequired(() =>
            {
                readDataCommand?.NotifyCanExecuteChanged();
                writeDataCommand?.NotifyCanExecuteChanged();
                okCommand?.NotifyCanExecuteChanged();
                cancelCommand?.NotifyCanExecuteChanged();
            });

        // The steps of a read or a write, in this order. Each one ends the task when it fails. First the reader is looked for, and a
        // reader that is not connected ends the task before a provider is asked for anything. Then what can be known without the
        // card is checked, so that a call that cannot be made never asks a provider for a key. Then the keys that the authentication
        // needs are obtained, all of them or none. Last the reader is called once. The key is never read from a property on the way
        // to the card except in AcquireKeysAsync. Only one operation runs at a time: they share the outcome and the error of the
        // task, and one that is asked for while another runs is not started.
        private async Task RunPageOperationAsync(bool write)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            {
                return;
            }

            byte[] data = null;

            try
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(AreInputsEnabled));
                NotifyTheButtons();
                lastOutcome = null;
                CurrentTaskErrorLevel = ERROR.Empty;

                using (var device = ReaderDevice.Instance)
                {
                    // The connection of a PC/SC reader says whether a card session is open, which the call opens itself: only the
                    // other readers are asked whether they are connected.
                    if (device == null || (!device.IsPcscReader && !device.IsConnected))
                    {
                        EndWithoutTheCard(ERROR.TransportError, 0);

                        return;
                    }

                    var refused = CheckBeforeTheCard(device, write, out data, out var pages);

                    if (refused != ERROR.NoError)
                    {
                        EndWithoutTheCard(refused, pages);

                        return;
                    }

                    var keys = await AcquireKeysAsync(KeysOfTheAuthentication());

                    if (keys.Failed)
                    {
                        ReportKeyUnavailable(keys);

                        return;
                    }

                    var access = new UltralightAccess(
                        ExpectedChip,
                        Authentication,
                        Authentication == UltralightAuth.UltralightC ? keys.Hex(SecureKeySlots.UltralightCKey)
                            : Authentication == UltralightAuth.Password ? keys.Hex(SecureKeySlots.UltralightPassword) : null,
                        Authentication == UltralightAuth.Password ? keys.Hex(SecureKeySlots.UltralightPack) : null,
                        AllowProtectedPages);

                    var result = write
                        ? await device.WriteMifareUltralightPagesAsync(access, StartPage, data)
                        : await device.ReadMifareUltralightPagesAsync(access, StartPage, pages);

                    ReportResult(result, write, pages);
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // The type of the exception and nothing else: its text can hold what was sent.
                Log.ForContext<MifareUltralightSetupViewModel>().Error("An Ultralight page operation failed ({ExceptionType})", e.GetType().FullName);
                EndWithoutTheCard(ERROR.Unknown, 0);
            }
            finally
            {
                if (data != null)
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
                }

                Interlocked.Exchange(ref running, 0);
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(AreInputsEnabled));
                NotifyTheButtons();
            }
        }

        private int running;

        /// <summary>Gets whether a page operation is running: a second one is not started meanwhile.</summary>
        [XmlIgnore]
        public bool IsRunning => Volatile.Read(ref running) != 0;

        // What is known without the card. The checks are those of the reader layer, made with the same code on the call without its
        // keys, and the reader is asked whether it can make such a call at all (a PC/SC reader has no password command and knows no
        // NTAG). The reader checks again against the chip that it finds, which is the one that counts.
        private ERROR CheckBeforeTheCard(ReaderDevice device, bool write, out byte[] data, out int pages)
        {
            data = null;
            pages = PageCount;

            // A box that holds no number: the task still has the number it had, which is not what the dialog shows.
            if (!ArePageInputsValid)
            {
                pages = 0;

                return ERROR.ProtocolConstraint;
            }

            if (write)
            {
                if (!UltralightPayload.TryParse(WriteDataHex, out data))
                {
                    pages = 0;

                    return ERROR.ProtocolConstraint;
                }

                pages = UltralightPayload.PageCount(data);
            }

            var withoutKeys = new UltralightAccess(ExpectedChip, UltralightAuth.None, null, null, AllowProtectedPages);

            using (var probe = write ? UltralightRequest.ForWrite(withoutKeys, StartPage, data) : UltralightRequest.ForRead(withoutKeys, StartPage, pages))
            {
                if (probe.Error != ERROR.NoError)
                {
                    return ERROR.ProtocolConstraint;
                }
            }

            // The key of an Ultralight C is not the password of an NTAG: authenticating needs a chip that has what is used.
            if (Authentication != UltralightAuth.None && !(ExpectedChip.TryGetVariant(out var variant) && UltralightRequest.AuthenticationFits(Authentication, variant)))
            {
                return ERROR.ProtocolConstraint;
            }

            // Asked with the way of authenticating that the task will use, and without its keys: no key is needed to know that.
            return device.SupportsUltralightAccess(new UltralightAccess(ExpectedChip, Authentication, null, null, AllowProtectedPages))
                ? ERROR.NoError
                : ERROR.ProtocolConstraint;
        }

        private KeyNeed[] KeysOfTheAuthentication()
        {
            switch (Authentication)
            {
                case UltralightAuth.UltralightC:
                    return new[] { Need(SecureKeySlots.UltralightCKey, SecureKeyAlgorithm.TripleDes2K) };

                case UltralightAuth.Password:
                    // Both or none: the PACK is what the chip answers a correct password with, and the reader checks it.
                    return new[] { Need(SecureKeySlots.UltralightPassword, SecureKeyAlgorithm.UltralightPassword), Need(SecureKeySlots.UltralightPack, SecureKeyAlgorithm.UltralightPack) };

                default:
                    return Array.Empty<KeyNeed>();
            }
        }

        // An Ultralight key belongs to no application and has no number.
        private static KeyNeed Need(string slot, SecureKeyAlgorithm algorithm) =>
            new KeyNeed(slot, algorithm, SecureKeyUsage.Authenticate, null, null);

        private void ReportResult(UltralightResult result, bool write, int pages)
        {
            var details = new Dictionary<string, object>
            {
                ["PagesRequested"] = pages,
                ["PagesDone"] = result.PagesDone,
                ["Authenticated"] = result.Authenticated,
            };

            if (result.FailedPage.HasValue)
            {
                details["FailedPage"] = result.FailedPage.Value;
            }

            if (result.Variant.HasValue)
            {
                details["Variant"] = (int)result.Variant.Value;
            }

            lastOutcome = details;

            if (!write)
            {
                LastReadStartPage = StartPage;
                LastReadData = (byte[])result.Data.Clone();

                // The grid shows what was read. It is changed on the thread of the window, which the run loop may not be on.
                if (IsReadTask)
                {
                    UiDispatcher.InvokeIfRequired(RebuildPageRows);
                }
            }

            CurrentTaskErrorLevel = result.Error;
            IsTaskCompletedSuccessfully = result.Succeeded;

            // Numbers and the name of the error: never the data, the keys or what the reader said.
            StatusText += result.Succeeded
                ? string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource(write ? "textBoxStatusUltralightWritten" : "textBoxStatusUltralightRead"), DateTime.Now, result.PagesDone, StartPage) + "\n"
                : string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("textBoxStatusUltralightFailed"), DateTime.Now, result.Error, result.PagesDone, pages) + "\n";
        }

        // The task ends without a call to the card: no reader, a call that cannot be made, or a fault.
        private void EndWithoutTheCard(ERROR error, int pages)
        {
            CurrentTaskErrorLevel = error;
            IsTaskCompletedSuccessfully = false;
            StatusText += string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("textBoxStatusUltralightFailed"), DateTime.Now, error, 0, pages) + "\n";
        }

        #region Key acquisition

        // Every command asks for the keys it hands to the reader first, here, and goes on only with what comes back. A slot with a key
        // binding gets its key from the provider and never from the property; a slot without one passes the property on, whatever it
        // holds, and the reader decides. A command asks for every key its authentication carries, all or none.

        private async Task<AcquiredKeys> AcquireKeysAsync(params KeyNeed[] needs)
        {
            // While a provider answers the dialog does not take input: what a person changes in the meantime would no longer be what
            // the keys were asked for.
            SetFetchingKeys(true);

            try
            {
                return await KeyAcquisition.AcquireAsync(needs, KeyBindings, SecureKeySlots.Ultralight, EmbeddedKeyOf, KeyResolutionScope.Current);
            }
            finally
            {
                SetFetchingKeys(false);
            }
        }

        private bool isFetchingKeys;

        /// <summary>Gets whether the keys of a command are being obtained from their providers; the dialog is locked meanwhile.</summary>
        [XmlIgnore]
        public bool IsFetchingKeys => isFetchingKeys;

        /// <summary>Gets whether the inputs of the dialog take input: they do not while an operation runs, which includes the time the keys are being obtained.</summary>
        [XmlIgnore]
        public bool AreInputsEnabled => !isFetchingKeys && !IsRunning;

        private void SetFetchingKeys(bool fetching)
        {
            if (isFetchingKeys == fetching)
            {
                return;
            }

            isFetchingKeys = fetching;
            OnPropertyChanged(nameof(IsFetchingKeys));
            OnPropertyChanged(nameof(AreInputsEnabled));
        }

        // Says that the keys could not be obtained: the task ends with KeyUnavailable and the line names the slot, never the reference
        // or the provider's own words. The command has made no card call.
        private void ReportKeyUnavailable(AcquiredKeys keys)
        {
            StatusText += string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetResource("textBoxStatusKeyUnavailable"), DateTime.Now, keys.Failure?.Slot ?? string.Empty) + "\n";
            CurrentTaskErrorLevel = ERROR.KeyUnavailable;
            IsTaskCompletedSuccessfully = false;
        }

        #endregion Key acquisition

        #region What the last run left

        /// <summary>Gets the bytes of the last read, 4 per page, in order: the pages that were read, also when the read stopped on the way. Not part of the project.</summary>
        [XmlIgnore]
        public byte[] LastReadData
        {
            get => lastReadData;
            private set
            {
                lastReadData = value;
                OnPropertyChanged();
            }
        }
        private byte[] lastReadData;

        /// <summary>Gets the page that the bytes of <see cref="LastReadData"/> start at. Not part of the project.</summary>
        [XmlIgnore]
        public int LastReadStartPage
        {
            get => lastReadStartPage;
            private set
            {
                lastReadStartPage = value;
                OnPropertyChanged();
            }
        }
        private int lastReadStartPage;

        private Dictionary<string, object> lastOutcome;

        /// <inheritdoc />
        string ITaskOutcomeDetails.OutcomeDetailsSection => "UltralightPages";

        /// <inheritdoc />
        IReadOnlyDictionary<string, object> ITaskOutcomeDetails.GetOutcomeDetails() =>
            lastOutcome ?? new Dictionary<string, object>();

        /// <inheritdoc />
        void ITaskOutcomeDetails.ClearOutcomeDetails() => lastOutcome = null;

        #endregion What the last run left

        #endregion Commands

        #region IUserDialogViewModel Implementation

        [XmlIgnore]
        public bool IsModal { get; private set; }

        public virtual void RequestClose()
        {
            if (OnCloseRequest != null)
            {
                OnCloseRequest(this);
            }
            else
            {
                Close();
            }
        }

        public event EventHandler DialogClosing;

        private RelayCommand okCommand;

        /// <summary>
        /// Gets the command of the OK button. It is off while a box of the pages holds no number, and while an operation runs: OK
        /// adds the task to the project and closes the dialog, and a write that still waits for its key would reach the card after that.
        /// </summary>
        public ICommand OKCommand => okCommand ??= new RelayCommand(Ok, () => ArePageInputsValid && !IsRunning);

        protected virtual void Ok()
        {
            if (OnOk != null)
            {
                OnOk(this);
            }
            else
            {
                Close();
            }
        }

        private RelayCommand cancelCommand;

        /// <summary>
        /// Gets the command of the Cancel button. It is off while an operation runs: closing the dialog leaves the operation running,
        /// and a write that still waits for its key would reach the card after the person pressed the button that says it will not.
        /// </summary>
        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(Cancel, () => !IsRunning);

        protected virtual void Cancel()
        {
            if (OnCancel != null)
            {
                OnCancel(this);
            }
            else
            {
                Close();
            }
        }

        public ICommand AuthCommand => new RelayCommand(Auth);

        protected virtual void Auth()
        {
            if (OnAuth != null)
            {
                OnAuth(this);
            }
            else
            {
                Close();
            }
        }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnOk { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnCancel { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnAuth { get; set; }

        [XmlIgnore]
        public Action<MifareUltralightSetupViewModel> OnCloseRequest { get; set; }

        public void Close()
        {
            DialogClosing?.Invoke(this, new EventArgs());
        }

        public void Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
        }

        #endregion IUserDialogViewModel Implementation

        #region Localization

        /// <summary>
        /// localization strings
        /// </summary>
        public string LocalizationResourceSet { get; set; }

        [XmlIgnore]
        public string Caption
        {
            get => _Caption;
            set
            {
                _Caption = value;
                OnPropertyChanged(nameof(Caption));
            }
        }
        private string _Caption;

        #endregion Localization
    }
}
