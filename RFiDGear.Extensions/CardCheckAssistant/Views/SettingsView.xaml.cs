using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The settings of the Card Check Assistant. It shows a <see cref="SettingsViewModel"/> and nothing else: it does not
    /// know its window. Whoever hosts it listens to <see cref="SettingsViewModel.CloseRequested"/> and asks
    /// <see cref="SettingsViewModel.TryRequestClose"/> before the window goes away.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other part is generated from the XAML.")]
    internal sealed partial class SettingsView : UserControl
    {
        private readonly SettingsViewModel _viewModel;

        /// <param name="viewModel">The state of the settings; the host of the view owns and disposes it.</param>
        internal SettingsView(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        /// <summary>The state of the settings.</summary>
        internal SettingsViewModel ViewModel => _viewModel;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Once, however often the view is loaded: the load itself ignores a second call.
            _ = _viewModel.LoadAsync();
        }
    }
}
