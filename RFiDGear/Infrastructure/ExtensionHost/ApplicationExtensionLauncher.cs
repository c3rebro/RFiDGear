#nullable enable
using RFiDGear.Contracts;
using RFiDGear.UI.UIExtensions.Interfaces;

using Serilog;

using System;
using System.Threading;
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
    /// Default <see cref="IApplicationExtensionLauncher"/>. An extension whose metadata names a page (<c>Uri</c>) keeps
    /// the original behavior: a frame navigates to it and the exported class is never created. Any other extension must
    /// implement <see cref="IApplicationUIExtension"/>: it gets the host service and builds its own content.
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
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var name = extension.Metadata.Name ?? string.Empty;
            Window? window = null;
            Action? disposeContent = null;

            try
            {
                // An extension that names a page keeps the original behavior exactly: the page is opened and its
                // exported class is never created, so a class that was fine as a bare marker cannot start failing.
                var pageUri = extension.Metadata.Uri;
                if (!string.IsNullOrWhiteSpace(pageUri))
                {
                    return _openLegacyWindow(pageUri, name);
                }

                // First activation: the exported class is created here, never while the menu is built or at start-up.
                // MEF does not remember a constructor that threw, so opening the entry again tries again.
                if (extension.Value is not IApplicationUIExtension factory)
                {
                    throw new InvalidOperationException(
                        "The extension names no page (Uri) and does not implement IApplicationUIExtension.");
                }

                var content = factory.CreateContent(host)
                    ?? throw new InvalidOperationException("CreateContent returned null.");

                // Content that already belongs to another element is not ours to show or dispose: another window may
                // still be displaying it. CreateContent must return new content on every call.
                if (content.Parent != null)
                {
                    throw new InvalidOperationException(
                        "CreateContent returned an element that already belongs to another element; it must return new content on every call.");
                }

                // From here on the content is ours, and it is disposed exactly once: when its window closes, or
                // right away if the window could not be opened.
                disposeContent = Once(() => DisposeContent(content, name));
                var cleanup = disposeContent;

                window = new Window
                {
                    Title = name,
                    Width = 900,
                    Height = 600,
                    Content = content
                };
                window.Closed += (_, _) => cleanup();
                window.Show();
                return window;
            }
            catch (Exception ex)
            {
                // Show can fail after the window exists and is visible (first layout runs inside it), so close it
                // instead of leaving an empty window that nobody holds; closing raises Closed, which disposes the
                // content. Calling the cleanup afterwards covers the failures that happen before any window exists.
                CloseQuietly(window, name);
                disposeContent?.Invoke();

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

        /// <summary>Wraps an action so that it runs at most once, however many times and from wherever it is called.</summary>
        private static Action Once(Action action)
        {
            var done = 0;
            return () =>
            {
                if (Interlocked.Exchange(ref done, 1) == 0)
                {
                    action();
                }
            };
        }

        private static void CloseQuietly(Window? window, string extensionName)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                // A window that failed while it was being shown may refuse to close; the content is still disposed.
                var error = new SanitizedException(ex);
                Logger.Warning(
                    "Closing the window of application extension {ExtensionName} failed: {ExceptionType}: {ExceptionMessage}",
                    extensionName, error.TypeName, error.Message);
            }
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
