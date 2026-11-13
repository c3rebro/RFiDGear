using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// The key manager: for each key of a section of a task, whether it is typed into the task or named by a reference to a key
    /// store, the key store, the name of the key, and, for the Windows Credential Manager, a box to store the key under that
    /// name. Changes are applied at once; the window only has to be closed. It asks the supplier of keys once a second while it
    /// is open, because a program that registers as supplier raises no event.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
    public partial class KeyManagerView : Window
    {
        private readonly KeyManagerViewModel _viewModel;
        private readonly DispatcherTimer _timer;

        /// <summary>Creates the window.</summary>
        /// <param name="viewModel">What the window shows. The window does not dispose it.</param>
        internal KeyManagerView(KeyManagerViewModel viewModel)
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
                // outlives the window until it is collected. The boxes of rows that are collapsed are cleared too.
                ClearKeyEntries(this);
            };
        }

        private static void ClearKeyEntries(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);

                if (child is PasswordBox box)
                {
                    box.Clear();
                }

                ClearKeyEntries(child);
            }
        }

        // The key is read from the entry of the row and the entry is cleared, whatever came of it: the digits do not stay in the
        // window. The entry is the only place the key is ever held; the row and its view model never see it as text.
        private void StoreKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: KeyManagerSlotRow row, Parent: Panel panel })
            {
                return;
            }

            var box = panel.Children.OfType<PasswordBox>().FirstOrDefault();

            if (box == null)
            {
                return;
            }

            using (var entered = box.SecurePassword)
            {
                row.StoreEntered(entered);
            }

            box.Clear();
        }

        private void OnCloseRequested(object sender, EventArgs e) => Close();
    }
}
