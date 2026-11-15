#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Services.SecureKeys;

using Serilog;

namespace RFiDGear.Services.Sam
{
    /// <summary>How the SAM setup of the menu is shown. The application uses a window, the tests a recording.</summary>
    internal interface ISamSetupLauncher
    {
        /// <summary>Shows the setup and returns when the person closed it.</summary>
        /// <param name="owner">The window it belongs to; <see langword="null"/> for none.</param>
        /// <param name="gate">The gate of the reader: the setup holds it while it is open, so that nothing else uses the reader.</param>
        /// <param name="coordinator">The coordinator of the operations: the setup takes its slot while it is open, and is not opened while an operation is in progress.</param>
        /// <param name="isReaderBusy">Whether the main window says that the reader is in use (the setup is not opened then).</param>
        /// <param name="setBusy">Tells the main window whether the reader is in use.</param>
        void Show(Window? owner, ReaderAccessGate gate, ExtensionOperationCoordinator coordinator, Func<bool?> isReaderBusy, Action<bool?> setBusy);
    }

    /// <summary>Where the setup is, as far as the person needs to know.</summary>
    internal enum SamSetupState
    {
        /// <summary>No MIFARE SAM AV3 was found in a PC/SC reader.</summary>
        NoReader,

        /// <summary>A SAM was found; the setup is not connected to it.</summary>
        Found,

        /// <summary>Connected to the SAM and its version is known; nothing is known about its activation.</summary>
        Connected,

        /// <summary>The SAM answered the host authentication with "not activated".</summary>
        NotActivated,

        /// <summary>The host authentication succeeded and nothing has failed since.</summary>
        Authenticated,
    }

    /// <summary>One entry of the list of stored keys: a reference, or the choice to type the key.</summary>
    internal sealed class SamKeyChoice
    {
        /// <summary>Creates the choice.</summary>
        /// <param name="reference">The reference of the stored key; empty for "enter the key below".</param>
        /// <param name="displayName">What the person reads.</param>
        internal SamKeyChoice(string reference, string displayName)
        {
            Reference = reference;
            DisplayName = displayName;
        }

        /// <summary>Gets the reference of the stored key; empty when the key is typed.</summary>
        public string Reference { get; }

        /// <summary>Gets what the person reads.</summary>
        public string DisplayName { get; }
    }

    /// <summary>
    /// What the SAM setup of the menu shows and does: the SAMs that were found, and the four steps (check, authenticate,
    /// activate, write a key entry) of <see cref="SamSetupService"/>, each of them one attempt. No key is ever held here: a key
    /// that is typed passes from the box of the window into a step as a <see cref="SecureString"/> and nowhere else; a key that
    /// is stored is named by its reference and read inside the step.
    /// </summary>
    internal sealed class SamSetupViewModel : ObservableObject, IDisposable
    {
        private const int FirstEntry = SamSetupService.FirstWritableEntry;
        private const int LastEntry = SamSetupService.LastWritableEntry;

        private readonly SamSetupService _service;
        private readonly WindowsCredentialKeyProvider _keyStore;
        private readonly Func<string, string> _text;
        private readonly RelayCommand _closeCommand;
        private readonly AsyncRelayCommand _refreshCommand;
        private readonly AsyncRelayCommand _checkCommand;

        private IReadOnlyList<string> _readerNames = Array.Empty<string>();
        private IReadOnlyList<SamKeyChoice> _storedKeys;
        private string? _selectedReader;
        private SamSetupState _state = SamSetupState.NoReader;
        private bool _masterUsesFactoryKey = true;
        private string _masterReference = string.Empty;
        private string _newKeyReference = string.Empty;
        private string _entryNumberText = FirstEntry.ToString(CultureInfo.InvariantCulture);
        private string _versionText = string.Empty;
        private string _statusText = string.Empty;
        private string _resultText = string.Empty;
        private bool _resultIsError;
        private int _running;
        private bool _disposed;

        /// <summary>Creates the view model; <see cref="Create"/> is the way to get one that reads the texts of the application.</summary>
        /// <param name="service">The steps.</param>
        /// <param name="keyStore">The Windows Credential Manager provider, asked for the names of the stored keys.</param>
        /// <param name="text">Gives the text of a resource key.</param>
        internal SamSetupViewModel(SamSetupService service, WindowsCredentialKeyProvider keyStore, Func<string, string> text)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _text = text ?? throw new ArgumentNullException(nameof(text));

            _storedKeys = new[] { new SamKeyChoice(string.Empty, _text("labelSamSetupEnterBelow")) };
            _closeCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => CanClose);
            _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRunning);
            _checkCommand = new AsyncRelayCommand(CheckAsync, () => CanCheck);
        }

        /// <summary>Raised when the person asks to close the window.</summary>
        internal event EventHandler? CloseRequested;

        /// <summary>Gets the readers with a MIFARE SAM AV3 in them.</summary>
        public IReadOnlyList<string> ReaderNames => _readerNames;

        /// <summary>Gets or sets the reader of the SAM that the steps work on. A change lets go of the connection.</summary>
        public string? SelectedReader
        {
            get => _selectedReader;
            set
            {
                if (string.Equals(_selectedReader, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedReader = value;
                OnPropertyChanged();

                if (_state >= SamSetupState.Connected)
                {
                    _service.Close();
                    _versionText = string.Empty;
                    OnPropertyChanged(nameof(VersionText));
                    SetState(SamSetupState.Found);
                }
                else
                {
                    NotifyButtons();
                }
            }
        }

        /// <summary>Gets where the setup is.</summary>
        public SamSetupState State => _state;

        /// <summary>Gets the sentence that says where the setup is.</summary>
        public string StateText => _text(_state switch
        {
            SamSetupState.NoReader => "labelSamSetupStateNoReader",
            SamSetupState.Found => "labelSamSetupStateFound",
            SamSetupState.Connected => "labelSamSetupStateConnected",
            SamSetupState.NotActivated => "labelSamSetupStateNotActivated",
            _ => "labelSamSetupStateAuthenticated",
        });

        /// <summary>Gets what the SAM said about its version, or empty before it was asked.</summary>
        public string VersionText => _versionText;

        /// <summary>Gets whether a step is running. While it is, nothing else can be started and the window cannot be closed.</summary>
        public bool IsRunning => Volatile.Read(ref _running) != 0;

        /// <summary>Gets whether the boxes may be edited.</summary>
        public bool AreInputsEnabled => !IsRunning;

        /// <summary>Gets whether the window may be closed: not while a card call runs.</summary>
        public bool CanClose => !IsRunning;

        /// <summary>Gets whether the SAM can be checked.</summary>
        public bool CanCheck => !IsRunning && !string.IsNullOrEmpty(_selectedReader);

        /// <summary>Gets whether the host authentication can be tried.</summary>
        public bool CanAuthenticate => !IsRunning && _state >= SamSetupState.Connected;

        /// <summary>Gets whether the SAM can be activated: it said that it was never activated.</summary>
        public bool CanActivate => !IsRunning && _state == SamSetupState.NotActivated;

        /// <summary>Gets whether a key can be written: authenticated, and the entry number is one that may be written.</summary>
        public bool CanWrite => !IsRunning && _state == SamSetupState.Authenticated && IsEntryNumberValid;

        /// <summary>Gets the stored keys that can be chosen, the choice "enter the key below" first.</summary>
        public IReadOnlyList<SamKeyChoice> StoredKeys => _storedKeys;

        /// <summary>Gets or sets whether the master key is the factory key: sixteen bytes of zero.</summary>
        public bool MasterUsesFactoryKey
        {
            get => _masterUsesFactoryKey;
            set
            {
                if (SetProperty(ref _masterUsesFactoryKey, value))
                {
                    OnPropertyChanged(nameof(MasterKeyIsTyped));
                }
            }
        }

        /// <summary>Gets or sets the reference of the stored master key; empty when it is typed.</summary>
        public string MasterReference
        {
            get => _masterReference;
            set
            {
                if (SetProperty(ref _masterReference, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(MasterKeyIsTyped));
                }
            }
        }

        /// <summary>Gets whether the master key comes from the box: not the factory key and not a stored one.</summary>
        public bool MasterKeyIsTyped => !_masterUsesFactoryKey && _masterReference.Length == 0;

        /// <summary>Gets or sets the reference of the stored key that is written; empty when it is typed.</summary>
        public string NewKeyReference
        {
            get => _newKeyReference;
            set
            {
                if (SetProperty(ref _newKeyReference, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(NewKeyIsTyped));
                }
            }
        }

        /// <summary>Gets whether the key that is written comes from the box.</summary>
        public bool NewKeyIsTyped => _newKeyReference.Length == 0;

        /// <summary>Gets or sets the number of the key entry that is written, as it was typed.</summary>
        public string EntryNumberText
        {
            get => _entryNumberText;
            set
            {
                if (SetProperty(ref _entryNumberText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(IsEntryNumberValid));
                    NotifyButtons();
                }
            }
        }

        /// <summary>Gets whether the entry number is one that may be written: 1 to 127.</summary>
        public bool IsEntryNumberValid => TryGetEntryNumber(out _);

        /// <summary>Gets what the steps said so far, one line each; the newest last.</summary>
        public string StatusText => _statusText;

        /// <summary>Gets what the last step said, without the time; empty before a step was run.</summary>
        public string ResultText => _resultText;

        /// <summary>Gets whether there is something to say about the last step.</summary>
        public bool HasResult => _resultText.Length > 0;

        /// <summary>Gets whether what the last step said is an error or a warning.</summary>
        public bool ResultIsError => _resultIsError;

        /// <summary>Gets the command that closes the window.</summary>
        public ICommand CloseCommand => _closeCommand;

        /// <summary>Gets the command that looks for SAMs again.</summary>
        public ICommand RefreshCommand => _refreshCommand;

        /// <summary>Gets the command that connects to the SAM and asks for its version.</summary>
        public ICommand CheckCommand => _checkCommand;

        /// <summary>Gets the title of the window.</summary>
        public string Title => _text("windowTitleSamSetup");

        /// <summary>Gets the sentence that says what the window is for.</summary>
        public string IntroText => _text("labelSamSetupIntro");

        /// <summary>Gets the label of the list of readers.</summary>
        public string ReaderLabel => _text("labelSamSetupReader");

        /// <summary>Gets the text of the button that looks for SAMs again.</summary>
        public string RefreshText => _text("buttonContentSamSetupRefresh");

        /// <summary>Gets the text of the button that checks the SAM.</summary>
        public string CheckText => _text("buttonContentSamSetupCheck");

        /// <summary>Gets the tooltip of the button that checks the SAM.</summary>
        public string CheckToolTip => _text("toolTipSamSetupCheck");

        /// <summary>Gets the header of the part of the master key.</summary>
        public string MasterHeader => _text("labelSamSetupMasterKey");

        /// <summary>Gets the text of the check box for the factory key.</summary>
        public string FactoryKeyText => _text("labelSamSetupFactoryKey");

        /// <summary>Gets the tooltip of the check box for the factory key.</summary>
        public string FactoryKeyToolTip => _text("toolTipSamSetupFactoryKey");

        /// <summary>Gets the label of the list of stored keys.</summary>
        public string StoredKeyLabel => _text("labelSamSetupStoredKey");

        /// <summary>Gets the tooltip of the list of stored keys.</summary>
        public string StoredKeyToolTip => _text("toolTipSamSetupStoredKey");

        /// <summary>Gets the label of the box of the key.</summary>
        public string KeyDigitsLabel => _text("labelSamSetupKeyDigits");

        /// <summary>Gets the text of the check box that shows the key that is entered.</summary>
        public string ShowKeyText => _text("labelKeyManagerShowKey");

        /// <summary>Gets the tooltip of the check box that shows the key that is entered.</summary>
        public string ShowKeyToolTip => _text("toolTipKeyManagerShowKey");

        /// <summary>Gets the text of the button that authenticates.</summary>
        public string AuthenticateText => _text("buttonContentSamSetupAuthenticate");

        /// <summary>Gets the tooltip of the button that authenticates.</summary>
        public string AuthenticateToolTip => _text("toolTipSamSetupAuthenticate");

        /// <summary>Gets the text of the button that activates the SAM.</summary>
        public string ActivateText => _text("buttonContentSamSetupActivate");

        /// <summary>Gets the tooltip of the button that activates the SAM.</summary>
        public string ActivateToolTip => _text("toolTipSamSetupActivate");

        /// <summary>Gets the header of the part of the key that is written.</summary>
        public string NewKeyHeader => _text("labelSamSetupNewKey");

        /// <summary>Gets the label of the key entry number.</summary>
        public string EntryLabel => _text("labelSamSetupEntry");

        /// <summary>Gets the tooltip of the key entry number.</summary>
        public string EntryToolTip => _text("toolTipSamSetupEntry");

        /// <summary>Gets the text of the button that writes the key.</summary>
        public string WriteText => _text("buttonContentSamSetupWrite");

        /// <summary>Gets the tooltip of the button that writes the key.</summary>
        public string WriteToolTip => _text("toolTipSamSetupWrite");

        /// <summary>Gets the label of the status.</summary>
        public string StatusLabel => _text("labelSamSetupStatus");

        /// <summary>Gets the text of the button that closes the window.</summary>
        public string CloseText => _text("buttonContentKeyManagerClose");

        /// <summary>Makes the view model.</summary>
        /// <param name="service">The steps.</param>
        /// <param name="keyStore">The Windows Credential Manager provider.</param>
        /// <param name="text">Gives the text of a resource key; <see langword="null"/> for the resources of the application.</param>
        /// <returns>The view model, which the caller disposes.</returns>
        internal static SamSetupViewModel Create(SamSetupService service, WindowsCredentialKeyProvider keyStore, Func<string, string>? text = null) =>
            // The resources read the settings from disk on every lookup; the language cannot change while the window is open.
            new SamSetupViewModel(service, keyStore, CachedText.Of(text ?? ResourceLoader.GetResource));

        /// <summary>Looks for SAMs in the PC/SC readers and reads the names of the stored keys again. Lets go of the connection.</summary>
        /// <returns>A task that ends when the search is done.</returns>
        internal async Task RefreshAsync()
        {
            await RunAsync(async () =>
            {
                var vaultError = ReadStoredKeys();

                var result = await _service.FindAsync();

                _readerNames = result.Readers.Select(reader => reader.Name).ToList();
                OnPropertyChanged(nameof(ReaderNames));
                _versionText = string.Empty;
                OnPropertyChanged(nameof(VersionText));

                if (_selectedReader == null || !_readerNames.Contains(_selectedReader))
                {
                    _selectedReader = _readerNames.FirstOrDefault();
                    OnPropertyChanged(nameof(SelectedReader));
                }

                SetState(_readerNames.Count > 0 ? SamSetupState.Found : SamSetupState.NoReader);

                if (result.EnumerationFailed)
                {
                    AppendStatus(_text("messageSamSetupSearchFailed"), true);
                }
                else if (_readerNames.Count == 0)
                {
                    AppendStatus(_text("messageSamSetupSearchNone"), false);
                }
                else
                {
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupSearchFound"), _readerNames.Count), false);
                }

                // Said last, so that it is not hidden by the good news about the search: the list of stored keys is what it was before.
                if (vaultError != 0)
                {
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageStoredKeysListFailed"), vaultError), true);
                }

                return true;
            });
        }

        /// <summary>Connects to the SAM of the selected reader and asks for its version. No key is used and nothing is changed.</summary>
        /// <returns>A task that ends when the SAM has answered.</returns>
        internal async Task CheckAsync()
        {
            var reader = _selectedReader;

            if (string.IsNullOrEmpty(reader))
            {
                return;
            }

            await RunAsync(async () =>
            {
                var outcome = await _service.ConnectAsync(reader);

                switch (outcome.Result)
                {
                    case SamStepResult.Done:
                        _versionText = outcome.Version == null
                            ? _text("messageSamSetupNoVersion")
                            : string.Format(CultureInfo.CurrentCulture, _text("labelSamSetupVersion"), outcome.Version.HardwareInfo, outcome.Version.SoftwareInfo);
                        OnPropertyChanged(nameof(VersionText));
                        AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupConnected"), reader), false);
                        SetState(SamSetupState.Connected);
                        break;
                    case SamStepResult.NoSam:
                        AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupNoSam"), reader), true);
                        SetState(SamSetupState.Found);
                        break;
                    default:
                        AppendStatus(_text("messageSamSetupReaderError"), true);
                        SetState(SamSetupState.Found);
                        break;
                }

                return true;
            });
        }

        /// <summary>Authenticates as host with the master key (entry 0, version 0): the factory key, a stored key or the typed one.</summary>
        /// <param name="typedMasterKey">What was typed into the box; used only when the key is neither the factory key nor a stored one.</param>
        /// <returns><see langword="true"/> when the SAM was asked and the typed digits are no longer needed.</returns>
        internal Task<bool> AuthenticateAsync(SecureString? typedMasterKey) =>
            RunAsync(async () =>
            {
                var source = MasterSource(typedMasterKey);

                if (source == null)
                {
                    AppendStatus(_text("messageSamSetupNoKeyEntered"), true);
                    return false;
                }

                var outcome = await _service.AuthenticateAsync(source);

                switch (outcome.Result)
                {
                    case SamStepResult.Done:
                        AppendStatus(_text("messageSamSetupAuthenticated"), false);
                        SetState(SamSetupState.Authenticated);
                        break;
                    case SamStepResult.NotActivated:
                        AppendStatus(_text("messageSamSetupNotActivated"), true);
                        SetState(SamSetupState.NotActivated);
                        break;
                    default:
                        ReportFailure(outcome);
                        break;
                }

                return KeyWasTried(outcome.Result);
            });

        /// <summary>
        /// Activates a new SAM. The service asks first; without a yes nothing is sent. The key is the master key, like for the
        /// authentication.
        /// </summary>
        /// <param name="typedMasterKey">What was typed into the box; used only when the key is neither the factory key nor a stored one.</param>
        /// <returns><see langword="true"/> when the SAM was asked and the typed digits are no longer needed.</returns>
        internal Task<bool> ActivateAsync(SecureString? typedMasterKey) =>
            RunAsync(async () =>
            {
                var source = MasterSource(typedMasterKey);

                if (source == null)
                {
                    AppendStatus(_text("messageSamSetupNoKeyEntered"), true);
                    return false;
                }

                var outcome = await _service.ActivateAsync(source);

                switch (outcome.Result)
                {
                    case SamStepResult.Done:
                        AppendStatus(_text("messageSamSetupActivated"), false);
                        SetState(SamSetupState.Connected);
                        break;
                    case SamStepResult.Declined:
                        AppendStatus(_text("messageSamSetupDeclined"), false);
                        break;
                    case SamStepResult.MayHaveTakenEffect:
                        AppendStatus(_text("messageSamSetupMayHaveActivated"), true);
                        break;
                    default:
                        ReportFailure(outcome);
                        break;
                }

                return KeyWasTried(outcome.Result);
            });

        /// <summary>Writes an AES-128 PICC key into the key entry that was chosen. Needs a host authentication first.</summary>
        /// <param name="typedKey">What was typed into the box; used only when the key is not a stored one.</param>
        /// <returns><see langword="true"/> when the SAM was asked and the typed digits are no longer needed.</returns>
        internal Task<bool> WriteKeyAsync(SecureString? typedKey) =>
            RunAsync(async () =>
            {
                if (!TryGetEntryNumber(out var entry))
                {
                    AppendStatus(_text("messageSamSetupEntryInvalid"), true);
                    return false;
                }

                SamKeySource? source = _newKeyReference.Length > 0
                    ? SamKeySource.FromStored(_newKeyReference)
                    : typedKey != null ? SamKeySource.FromTyped(typedKey) : null;

                if (source == null)
                {
                    AppendStatus(_text("messageSamSetupNoKeyEntered"), true);
                    return false;
                }

                var outcome = await _service.WriteKeyEntryAsync(entry, source);

                if (outcome.Result == SamStepResult.Done)
                {
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupWritten"), entry), false);
                }
                else if (outcome.Result == SamStepResult.MayHaveTakenEffect)
                {
                    // The SAM said success, and its answer could not be checked: not a refusal, and nothing says that it was written.
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupMayHaveWritten"), entry), true);
                }
                else
                {
                    ReportFailure(outcome);
                }

                return KeyWasTried(outcome.Result);
            });

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.Dispose();
        }

        private static bool KeyWasTried(SamStepResult result) => result is
            SamStepResult.Done or SamStepResult.NotActivated or SamStepResult.AuthFailed or SamStepResult.Refused
            or SamStepResult.ReaderError or SamStepResult.MayHaveTakenEffect or SamStepResult.Failed;

        private SamKeySource? MasterSource(SecureString? typed)
        {
            if (_masterUsesFactoryKey)
            {
                return SamKeySource.Factory();
            }

            if (_masterReference.Length > 0)
            {
                return SamKeySource.FromStored(_masterReference);
            }

            return typed != null ? SamKeySource.FromTyped(typed) : null;
        }

        private bool TryGetEntryNumber(out int entry) =>
            int.TryParse(_entryNumberText, NumberStyles.None, CultureInfo.InvariantCulture, out entry) && entry >= FirstEntry && entry <= LastEntry;

        // What a step that did not work says. The status word is the SAM's own answer; nothing else of the SAM is shown.
        private void ReportFailure(SamStepOutcome outcome)
        {
            var status = outcome.StatusWord.ToString("X4", CultureInfo.InvariantCulture);

            switch (outcome.Result)
            {
                case SamStepResult.AuthFailed:
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupAuthFailed"), status), true);
                    break;
                case SamStepResult.Refused:
                    AppendStatus(string.Format(CultureInfo.CurrentCulture, _text("messageSamSetupRefused"), status), true);
                    break;
                case SamStepResult.KeyUnavailable:
                    AppendStatus(_text("messageSamSetupKeyUnavailable"), true);
                    break;
                case SamStepResult.InvalidKey:
                    AppendStatus(_text("messageSamSetupKeyInvalid"), true);
                    break;
                case SamStepResult.NotAuthenticated:
                    AppendStatus(_text("messageSamSetupNotAuthenticated"), true);
                    break;
                case SamStepResult.InvalidEntry:
                    AppendStatus(_text("messageSamSetupEntryInvalid"), true);
                    break;
                case SamStepResult.NoSam:
                    AppendStatus(_text("messageSamSetupNotConnected"), true);
                    break;
                case SamStepResult.ReaderError:
                    AppendStatus(_text("messageSamSetupReaderError"), true);
                    break;
                default:
                    AppendStatus(_text("messageSamSetupFailed"), true);
                    break;
            }
        }

        // Runs one step. A second step cannot start while one runs, and what the step did to the connection decides the state.
        private async Task<bool> RunAsync(Func<Task<bool>> step)
        {
            if (_disposed || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return false;
            }

            RunningChanged();

            try
            {
                return await step();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Nothing the services throw is expected here; the type goes to the log and the person gets a fixed sentence.
                Log.ForContext<SamSetupViewModel>().Warning("SAM setup: a step failed ({ExceptionType}).", e.GetType().Name);
                _service.Close();
                AppendStatus(_text("messageSamSetupFailed"), true);
                return false;
            }
            finally
            {
                // A connection that a step ended is not a state that the buttons may still offer.
                if (!_service.IsConnected && _state >= SamSetupState.Connected)
                {
                    SetState(_readerNames.Count > 0 ? SamSetupState.Found : SamSetupState.NoReader);
                }
                else if (_state == SamSetupState.Authenticated && !_service.IsAuthenticated)
                {
                    SetState(SamSetupState.Connected);
                }

                Volatile.Write(ref _running, 0);
                RunningChanged();
            }
        }

        private void RunningChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(AreInputsEnabled));
            NotifyButtons();
        }

        private void SetState(SamSetupState state)
        {
            _state = state;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(StateText));
            NotifyButtons();
        }

        private void NotifyButtons()
        {
            OnPropertyChanged(nameof(CanCheck));
            OnPropertyChanged(nameof(CanAuthenticate));
            OnPropertyChanged(nameof(CanActivate));
            OnPropertyChanged(nameof(CanWrite));
            OnPropertyChanged(nameof(CanClose));

            _closeCommand.NotifyCanExecuteChanged();
            _refreshCommand.NotifyCanExecuteChanged();
            _checkCommand.NotifyCanExecuteChanged();
        }

        private void AppendStatus(string line, bool isError)
        {
            _statusText += DateTime.Now.ToString("T", CultureInfo.CurrentCulture) + "  " + line + "\n";
            _resultText = line;
            _resultIsError = isError;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(ResultIsError));
        }

        // Returns the error of the Credential Manager, or 0. A vault that cannot be listed now is no proof that the keys are gone: the
        // choices and what was chosen stay as they were, and the caller says why nothing new is shown.
        private int ReadStoredKeys()
        {
            var list = _keyStore.List();

            if (list.Error != 0)
            {
                return list.Error;
            }

            var choices = new List<SamKeyChoice> { new SamKeyChoice(string.Empty, _text("labelSamSetupEnterBelow")) };
            choices.AddRange(list.References.Select(reference => new SamKeyChoice(reference, reference)));
            _storedKeys = choices;
            OnPropertyChanged(nameof(StoredKeys));

            // A key that was stored when it was chosen and is gone now is not a choice any more.
            if (_masterReference.Length > 0 && !list.References.Contains(_masterReference))
            {
                MasterReference = string.Empty;
            }

            if (_newKeyReference.Length > 0 && !list.References.Contains(_newKeyReference))
            {
                NewKeyReference = string.Empty;
            }

            return 0;
        }
    }
}
