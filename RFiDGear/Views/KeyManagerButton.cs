#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RFiDGear.Infrastructure;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>Shows the key manager as a modal window of the window that holds the control.</summary>
    internal sealed class WpfKeyManagerLauncher : IKeyManagerLauncher
    {
        /// <inheritdoc />
        public void Show(KeyManagerViewModel viewModel, Window? owner)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            new KeyManagerView(viewModel) { Owner = owner }.ShowDialog();
        }
    }

    /// <summary>
    /// The button at the bottom of a section of keys that opens the key manager for the keys of that section. It needs no list
    /// of slots: it takes the key slot editors that are visible in its section (by default the group box it sits in), so a key
    /// that a task type hides is not offered, and a section with no visible key opens nothing.
    /// </summary>
    public class KeyManagerButton : Button
    {
        /// <summary>Identifies <see cref="Scope"/>.</summary>
        public static readonly DependencyProperty ScopeProperty = DependencyProperty.Register(
            nameof(Scope), typeof(FrameworkElement), typeof(KeyManagerButton), new PropertyMetadata(null));

        /// <summary>Creates the button with the look of the buttons at the bottom of a group box.</summary>
        public KeyManagerButton()
        {
            Height = 26;
            Padding = new Thickness(5, 2, 5, 2);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Bottom;
            Content = ResourceLoader.GetResource("buttonContentKeyManager");

            // A tooltip given as text is one line: this one wraps.
            ToolTip = new ToolTip
            {
                MaxWidth = 380,
                Content = new TextBlock { Text = ResourceLoader.GetResource("toolTipKeyManager"), TextWrapping = TextWrapping.Wrap },
            };

            Click += (_, _) => Open();
        }

        /// <summary>
        /// Gets or sets the part of the window whose key slot editors the manager is opened for; by default the group box that
        /// holds the button, or the window when it is in none.
        /// </summary>
        public FrameworkElement? Scope
        {
            get => (FrameworkElement?)GetValue(ScopeProperty);
            set => SetValue(ScopeProperty, value);
        }

        /// <summary>Gets or sets how the manager is shown; the tests replace it.</summary>
        internal IKeyManagerLauncher Launcher { get; set; } = new WpfKeyManagerLauncher();

        /// <summary>Gets or sets where the key stores come from; the tests replace it.</summary>
        internal Func<IReadOnlyList<SecureKeyProviderInfo>> Providers { get; set; } =
            () => KeyResolutionScope.Current?.Registry.Providers ?? (IReadOnlyList<SecureKeyProviderInfo>)Array.Empty<SecureKeyProviderInfo>();

        /// <summary>Gets or sets where a key is stored; the tests replace it.</summary>
        internal Func<WindowsCredentialKeyProvider> KeyStore { get; set; } = () => new WindowsCredentialKeyProvider(new NativeCredentialStore());

        /// <summary>Gets or sets what tells whether a program can supply keys; the tests replace it.</summary>
        internal IKeySupplierStatus SupplierStatus { get; set; } = new BridgeKeySupplierStatus();

        /// <summary>Gets or sets the texts; <see langword="null"/> for the resources of the application. The tests replace it.</summary>
        internal Func<string, string>? Text { get; set; }

        /// <summary>
        /// Finds the key slot editors the manager is for: those in <see cref="Scope"/> that are visible, in the order they stand
        /// on the screen (top to bottom, then left to right).
        /// </summary>
        /// <returns>The editors, which may be none.</returns>
        internal IReadOnlyList<KeySlotEditor> FindEditors()
        {
            FrameworkElement? scope = Scope ?? NearestGroupBox();
            scope ??= Window.GetWindow(this);

            if (scope == null)
            {
                return Array.Empty<KeySlotEditor>();
            }

            return Descendants(scope)
                .OfType<KeySlotEditor>()
                .Where(editor => editor.IsVisible && editor.Host is IKeySlotHost && !string.IsNullOrEmpty(editor.SlotId))
                .OrderBy(editor => PositionIn(scope, editor).Y)
                .ThenBy(editor => PositionIn(scope, editor).X)
                .ToList();
        }

        /// <summary>Opens the key manager for the visible keys of the section, if there are any.</summary>
        /// <returns><see langword="true"/> when the manager was shown.</returns>
        internal bool Open()
        {
            var editors = FindEditors();

            if (editors.Count == 0 || editors[0].Host is not IKeySlotHost host)
            {
                return false;
            }

            var window = Window.GetWindow(this);
            var slotIds = editors.Select(editor => editor.SlotId).ToList();

            using var viewModel = KeyManagerViewModel.Create(
                host,
                slotIds,
                Providers(),
                new WpfKeySlotEditorDialogs(() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive) ?? window),
                KeyStore(),
                SupplierStatus,
                Text,
                action => Dispatcher.BeginInvoke(action));

            Launcher.Show(viewModel, window);

            return true;
        }

        private GroupBox? NearestGroupBox()
        {
            for (var parent = VisualTreeHelper.GetParent(this); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is GroupBox box)
                {
                    return box;
                }
            }

            return null;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);

                yield return child;

                foreach (var nested in Descendants(child))
                {
                    yield return nested;
                }
            }
        }

        private static Point PositionIn(DependencyObject scope, Visual editor)
        {
            try
            {
                return editor.TransformToAncestor((Visual)scope).Transform(new Point(0, 0));
            }
            catch (InvalidOperationException)
            {
                // Not related to the scope any more (it was removed while we looked): the order does not matter then.
                return new Point(0, 0);
            }
        }
    }
}
