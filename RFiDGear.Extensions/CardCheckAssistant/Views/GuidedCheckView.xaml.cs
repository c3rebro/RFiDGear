using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// The window of the guided check: one section for each step, bound to a <see cref="GuidedCheckViewModel"/>. It
    /// starts what the page does once when it is first shown, and asks the page whether the window may close.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other part is generated from the XAML.")]
    internal sealed partial class GuidedCheckView : UserControl, IDisposable
    {
        private readonly GuidedCheckViewModel _viewModel;
        private Window? _window;
        private bool _disposed;

        /// <summary>Creates the view and binds it to its page.</summary>
        /// <param name="viewModel">The page; the view disposes it with itself.</param>
        /// <exception cref="ArgumentNullException"><paramref name="viewModel"/> is null.</exception>
        internal GuidedCheckView(GuidedCheckViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            DataContext = viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>Gets the page this view shows.</summary>
        internal GuidedCheckViewModel ViewModel => _viewModel;

        /// <summary>Stops listening to the window and disposes the page. Safe to call more than once.</summary>
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
            if (!_disposed)
            {
                // Once, however often the view is loaded: the import offer, the first read of the list, the timer.
                _ = _viewModel.Open();
            }

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
            // Closing would walk away from a step that cannot be left, and a check that can be abandoned is asked about.
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
