#nullable enable
using RFiDGear.Contracts;
using RFiDGear.UI.UIExtensions.Interfaces;

using Serilog;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Opens an application-level extension (<c>HostPlacement.Application</c>) in its own window.
    /// </summary>
    public interface IApplicationExtensionLauncher
    {
        /// <summary>
        /// Opens the extension. Call on the UI thread. It does not throw: a failure is logged, reported to the user
        /// through the error presenter and answered with <c>null</c>.
        /// </summary>
        /// <param name="extension">The lazily created extension and its metadata.</param>
        /// <param name="host">The initialized host service handed to the extension. Not <c>null</c>.</param>
        /// <returns>The window that was opened, or <c>null</c> if none was.</returns>
        Window? Open(Lazy<IUIExtension, IUIExtensionDetails> extension, IRFiDGearExtensionHost host);
    }

    /// <summary>
    /// Default <see cref="IApplicationExtensionLauncher"/>. An extension that implements
    /// <see cref="IApplicationUIExtension"/> gets the host service and builds its own content; any other extension keeps
    /// the original behavior of navigating a frame to the URI in its metadata.
    /// </summary>
    internal sealed class ApplicationExtensionLauncher : IApplicationExtensionLauncher
    {
        private static ILogger Logger => Log.ForContext<ApplicationExtensionLauncher>();

        private readonly Action<string> _showError;
        private readonly Func<string, string, Window?> _openLegacyWindow;

        /// <param name="showError">Tells the user an extension could not be opened; receives the extension's name.</param>
        /// <param name="openLegacyWindow">
        /// Opens a window that navigates to a URI (URI, title). Defaults to the original frame-based window; tests
        /// replace it so no navigation happens.
        /// </param>
        public ApplicationExtensionLauncher(Action<string> showError, Func<string, string, Window?>? openLegacyWindow = null)
        {
            _showError = showError ?? throw new ArgumentNullException(nameof(showError));
            _openLegacyWindow = openLegacyWindow ?? OpenFrameWindow;
        }

        /// <inheritdoc/>
        public Window? Open(Lazy<IUIExtension, IUIExtensionDetails> extension, IRFiDGearExtensionHost host)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));
            if (host == null) throw new ArgumentNullException(nameof(host));

            var name = extension.Metadata.Name ?? string.Empty;
            FrameworkElement? content = null;
            var shown = false;

            try
            {
                // First activation: the exported class is created here, never while the menu is built or at start-up.
                // MEF does not remember a constructor that threw, so opening the entry again tries again.
                if (extension.Value is IApplicationUIExtension factory)
                {
                    content = factory.CreateContent(host)
                        ?? throw new InvalidOperationException("CreateContent returned null.");

                    var window = new Window
                    {
                        Title = name,
                        Width = 900,
                        Height = 600,
                        Content = content
                    };

                    var created = content;
                    window.Closed += (_, _) => DisposeContent(created, name);
                    window.Show();
                    shown = true;
                    return window;
                }

                return _openLegacyWindow(extension.Metadata.Uri, name);
            }
            catch (Exception ex)
            {
                // A window that never showed will never raise Closed, so its content is cleaned up here.
                if (!shown)
                {
                    DisposeContent(content, name);
                }

                var error = new SanitizedException(ex);
                Logger.Error(
                    "Application extension {ExtensionName} could not be opened: {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                    name, error.TypeName, error.Message, error.StackTrace);
                ShowError(name);
                return null;
            }
        }

        /// <summary>
        /// The original behavior for an extension that only names a page: a window with a frame navigated to the URI.
        /// </summary>
        private static Window OpenFrameWindow(string uri, string title)
        {
            var frame = new Frame
            {
                NavigationUIVisibility = NavigationUIVisibility.Hidden
            };
            var window = new Window
            {
                Title = title ?? string.Empty,
                Width = 900,
                Height = 600,
                Content = frame
            };
            frame.Source = new Uri(uri);
            window.Show();
            return window;
        }

        /// <summary>
        /// Disposes what the extension created for one window: the content, and a different object that is its
        /// <see cref="FrameworkElement.DataContext"/>. Each is disposed once and a failing <c>Dispose</c> is only logged.
        /// </summary>
        private static void DisposeContent(FrameworkElement? content, string extensionName)
        {
            if (content == null)
            {
                return;
            }

            var dataContext = content.DataContext as IDisposable;
            TryDispose(content as IDisposable, extensionName);

            if (dataContext != null && !ReferenceEquals(dataContext, content))
            {
                TryDispose(dataContext, extensionName);
            }
        }

        private static void TryDispose(IDisposable? disposable, string extensionName)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                var error = new SanitizedException(ex);
                Logger.Error(
                    "Disposing the content of application extension {ExtensionName} failed: {ExceptionType}: {ExceptionMessage} {ExceptionStackTrace}",
                    extensionName, error.TypeName, error.Message, error.StackTrace);
            }
        }

        private void ShowError(string extensionName)
        {
            try
            {
                _showError(extensionName);
            }
            catch (Exception ex)
            {
                var error = new SanitizedException(ex);
                Logger.Error(
                    "Reporting the failure of application extension {ExtensionName} failed: {ExceptionType}: {ExceptionMessage}",
                    extensionName, error.TypeName, error.Message);
            }
        }
    }
}
