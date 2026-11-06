using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The page of the Card Check Assistant. It keeps its window from closing in the middle of a run and, when the host
    /// disposes it after the window closed, lets go of everything it listened to.
    /// </summary>
    internal sealed partial class CardCheckAssistantView : UserControl, IDisposable
    {
        private readonly CardCheckAssistantViewModel _viewModel;
        private Window? _window;
        private bool _disposed;

        /// <param name="viewModel">The state of this page; the view owns it and disposes it.</param>
        internal CardCheckAssistantView(CardCheckAssistantViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>The state of this page.</summary>
        internal CardCheckAssistantViewModel ViewModel => _viewModel;

        /// <summary>Stops listening to the window and releases the view model. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            DetachFromWindow();
            _viewModel.Dispose();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (_disposed || window == null || ReferenceEquals(window, _window))
            {
                return;
            }

            DetachFromWindow();
            _window = window;
            _window.Closing += OnWindowClosing;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => DetachFromWindow();

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            // Closing would walk away from a card operation in the middle of it, so the window stays until it is done.
            if (!_viewModel.TryRequestClose())
            {
                e.Cancel = true;
            }
        }

        private void DetachFromWindow()
        {
            if (_window != null)
            {
                _window.Closing -= OnWindowClosing;
                _window = null;
            }
        }
    }
}
