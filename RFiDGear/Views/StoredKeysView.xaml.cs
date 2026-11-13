using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// The key manager of the menu: the keys that are stored in the Windows Credential Manager, by name; a way to store a key under
    /// a name; and a way to remove one. It asks the supplier of keys once a second while it is open, because a program that
    /// registers as supplier raises no event.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class StoredKeysView : Window
    {
        private readonly StoredKeysViewModel _viewModel;
        private readonly DispatcherTimer _timer;

        /// <summary>Creates the window.</summary>
        /// <param name="viewModel">What the window shows. The window does not dispose it.</param>
        internal StoredKeysView(StoredKeysViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => _viewModel.RefreshSupplierStatus();
            _viewModel.CloseRequested += OnCloseRequested;

            Loaded += (_, _) => _timer.Start();
            Closed += (_, _) =>
            {
                _timer.Stop();
                _viewModel.CloseRequested -= OnCloseRequested;

                // Close, Escape and the title bar all end here: a key that was typed and not stored does not stay in a box that
                // outlives the window until it is collected.
                KeyBox.Clear();
            };
        }

        // The key is read from the box and the box is cleared when a key was tried, whatever came of it: the digits do not stay in
        // the window. When nothing was stored (the name is not valid, the entry is not a key, or the person declined to replace a
        // key) the digits stay, so that the person can go on with the entry. The box is the only place the key is ever held; the
        // view model never sees it as text.
        private void StoreKey_Click(object sender, RoutedEventArgs e) => Store();

        // Enter in the box of the key stores it when it can be stored, and does nothing otherwise. It never closes the window:
        // closing clears the box, and a key that was typed would be thrown away without a word.
        private void KeyBox_EnterPressed(object sender, EventArgs e) => Store();

        private void Store()
        {
            if (!_viewModel.CanStoreKey || !KeyBox.IsKeyValid)
            {
                return;
            }

            bool tried;

            using (var entered = KeyBox.GetEntered())
            {
                tried = _viewModel.StoreEntered(entered);
            }

            if (tried)
            {
                KeyBox.Clear();
            }
        }

        private void OnCloseRequested(object sender, EventArgs e) => Close();
    }
}
