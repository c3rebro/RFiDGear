#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Threading.Tasks;
using System.Windows;

using RFiDGear.Services.Sam;

using Serilog;

namespace RFiDGear.View
{
    /// <summary>
    /// The setup of a MIFARE SAM AV3 of the menu: the SAMs that were found in the PC/SC readers, the host authentication with the
    /// master key, the activation of a new SAM, and writing one key into a key entry. A key that is typed is read from its box
    /// and handed to the step as a secure string; the box is cleared when the SAM was asked, whatever came of it.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class SamSetupView : Window
    {
        private readonly SamSetupViewModel _viewModel;

        /// <summary>Creates the window.</summary>
        /// <param name="viewModel">What the window shows. The window does not dispose it.</param>
        internal SamSetupView(SamSetupViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            _viewModel.CloseRequested += OnCloseRequested;

            // The search takes a moment (an empty reader is waited for): it does not hold the window up.
            Loaded += async (_, _) => await Guarded(_viewModel.RefreshAsync());
            Closing += OnClosing;
            Closed += (_, _) =>
            {
                _viewModel.CloseRequested -= OnCloseRequested;

                // Close, Escape and the title bar all end here: a key that was typed does not stay in a box that outlives the window
                // until it is collected.
                MasterKeyBox.Clear();
                NewKeyBox.Clear();
            };
        }

        private void Authenticate_Click(object sender, RoutedEventArgs e) => _ = Authenticate();

        private void Activate_Click(object sender, RoutedEventArgs e) => _ = Activate();

        private void Write_Click(object sender, RoutedEventArgs e) => _ = Write();

        // Enter in a box does what its button does, when that can be done. It never closes the window: closing clears the box, and
        // a key that was typed would be thrown away without a word.
        private void MasterKeyBox_EnterPressed(object sender, EventArgs e)
        {
            if (_viewModel.CanAuthenticate)
            {
                _ = Authenticate();
            }
        }

        private void NewKeyBox_EnterPressed(object sender, EventArgs e)
        {
            if (_viewModel.CanWrite)
            {
                _ = Write();
            }
        }

        private Task Authenticate() => Guarded(WithKey(MasterKeyBox, _viewModel.MasterKeyIsTyped, _viewModel.AuthenticateAsync));

        private Task Activate() => Guarded(WithKey(MasterKeyBox, _viewModel.MasterKeyIsTyped, _viewModel.ActivateAsync));

        private Task Write() => Guarded(WithKey(NewKeyBox, _viewModel.NewKeyIsTyped, _viewModel.WriteKeyAsync));

        // The key is read from the box and the box is cleared when the SAM was asked, whatever came of it: the digits do not stay in
        // the window. When nothing was sent (the entry is not a key, the person said no) the digits stay, so that the person can go
        // on with the entry. The box is the only place a typed key is held; the view model never sees it as text.
        private static async Task WithKey(KeyEntryBox box, bool typed, Func<SecureString?, Task<bool>> step)
        {
            if (!typed)
            {
                await step(null);
                return;
            }

            using var entered = box.GetEntered();

            if (await step(entered))
            {
                box.Clear();
            }
        }

        // A handler of the window that does not return a task must not let an exception go: it would end the application.
        private static async Task Guarded(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log.ForContext<SamSetupView>().Warning("SAM setup: the window could not run a step ({ExceptionType}).", e.GetType().Name);
            }
        }

        // The window cannot be closed while a card call runs: the reader would be taken away in the middle of a command.
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.CanClose)
            {
                e.Cancel = true;
            }
        }

        private void OnCloseRequested(object? sender, EventArgs e) => Close();
    }
}
