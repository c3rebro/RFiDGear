using RFiDGear.Infrastructure.Tasks;
using RFiDGear.ViewModel.TaskSetupViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Shows a tab of the DESFire task dialog in a window, bound to a view model, so that a test can ask whether a
    /// control is really visible and enabled. Controls are found in the logical tree by what they are bound to, so one in
    /// a group that is collapsed is still found, and fails because it is not visible and not because it is missing.
    /// Text is not asserted: it follows the language.
    /// </summary>
    internal static class RenderedTab
    {
        /// <summary>
        /// Shows <typeparamref name="TView"/> bound to a new view model and lets <paramref name="inspect"/> look at it.
        /// </summary>
        /// <typeparam name="TView">The tab to show.</typeparam>
        /// <param name="taskType">The task the view model is set to.</param>
        /// <param name="selectTypeAfterRender">
        /// <see langword="false"/> to set the task before the tab is shown (a task that is edited), <see langword="true"/>
        /// to set it after (a task that is picked in the dialog).
        /// </param>
        /// <param name="inspect">What to check, given the tab and its view model.</param>
        /// <returns>A task that completes when the check has run and the window is closed.</returns>
        internal static async Task InspectAsync<TView>(
            TaskType_MifareDesfireTask taskType,
            bool selectTypeAfterRender,
            Action<FrameworkElement, MifareDesfireSetupViewModel> inspect)
            where TView : FrameworkElement, new()
        {
            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var viewModel = new MifareDesfireSetupViewModel();
                if (!selectTypeAfterRender)
                {
                    viewModel.SelectedTaskType = taskType;
                }

                var view = new TView { DataContext = viewModel };
                var window = new Window { Content = view, Width = 1100, Height = 800, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    if (selectTypeAfterRender)
                    {
                        viewModel.SelectedTaskType = taskType;
                    }

                    window.UpdateLayout();
                    await Flush();
                    window.UpdateLayout();

                    inspect(view, viewModel);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        /// <summary>Lets everything already queued on the dispatcher run.</summary>
        /// <returns>A task that completes when the dispatcher is idle for a moment.</returns>
        internal static async Task Flush() => await Dispatcher.Yield(DispatcherPriority.Background);

        /// <summary>
        /// Lists everything below an element in the logical tree. The logical tree, not the visual one: the contents of a
        /// group that is collapsed are never given a visual.
        /// </summary>
        /// <param name="root">The element to start from.</param>
        /// <returns>All descendants.</returns>
        internal static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;

                foreach (var nested in LogicalDescendants(child))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>Gets the path an element's property is bound to.</summary>
        /// <param name="element">The element.</param>
        /// <param name="property">The bound property.</param>
        /// <returns>The path, or an empty string when the property is not bound.</returns>
        internal static string BindingPath(DependencyObject element, DependencyProperty property) =>
            BindingOperations.GetBinding(element, property)?.Path?.Path ?? string.Empty;

        /// <summary>Finds the elements of a type whose property is bound to a path.</summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="root">Where to look.</param>
        /// <param name="property">The bound property.</param>
        /// <param name="path">The path it is bound to.</param>
        /// <returns>The matching elements.</returns>
        internal static List<T> Bound<T>(DependencyObject root, DependencyProperty property, string path)
            where T : FrameworkElement =>
            LogicalDescendants(root).OfType<T>().Where(element => BindingPath(element, property) == path).ToList();

        /// <summary>Finds the one element of a type whose property is bound to a path.</summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="root">Where to look.</param>
        /// <param name="property">The bound property.</param>
        /// <param name="path">The path it is bound to.</param>
        /// <returns>The element; the test fails when there is not exactly one.</returns>
        internal static T One<T>(DependencyObject root, DependencyProperty property, string path)
            where T : FrameworkElement
        {
            var found = Bound<T>(root, property, path);
            Assert.True(found.Count == 1, $"expected one {typeof(T).Name} bound to {path}, found {found.Count}");

            return found[0];
        }

        /// <summary>Fails the test unless the element is visible and enabled.</summary>
        /// <param name="element">The element.</param>
        /// <param name="what">What it is, for the failure message.</param>
        internal static void AssertUsable(FrameworkElement element, string what)
        {
            Assert.True(element.IsVisible, what + " is not visible");
            Assert.True(element.IsEnabled, what + " is not enabled");
        }

        /// <summary>
        /// Lists the captions that are shown and have no text. A resource key that does not exist gives an empty caption
        /// and no error, and a binding that fails gives none at all, so a label can silently go blank.
        /// </summary>
        /// <param name="view">The tab.</param>
        /// <returns>A description of each blank caption.</returns>
        internal static List<string> BlankCaptions(FrameworkElement view)
        {
            var blank = new List<string>();
            foreach (var element in LogicalDescendants(view).OfType<FrameworkElement>().Where(element => element.IsVisible))
            {
                // A label, a button, a check box or a radio button has text; a broken binding leaves none at all.
                if ((element is Label || element is ButtonBase) && ((ContentControl)element).Content == null)
                {
                    blank.Add(element.GetType().Name + " content");
                }

                if (element is ContentControl content && content.Content is string text && string.IsNullOrWhiteSpace(text))
                {
                    blank.Add(element.GetType().Name + " content");
                }

                if (element is HeaderedContentControl headered && (headered.Header is string header ? string.IsNullOrWhiteSpace(header) : headered.Header == null))
                {
                    blank.Add(element.GetType().Name + " header");
                }
            }

            return blank;
        }
    }
}
