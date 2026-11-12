using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// Wraps the box in which a key is typed and adds the choice to name the key instead: a reference to a key in a key
    /// store, which the task looks up when it runs. In the first mode the wrapped box is shown and works as it always did.
    /// In the second the project holds only the reference, a box for it takes the place of the key, and the key can be
    /// stored under that reference (for the Windows Credential Manager) without ever being shown.
    /// <para>
    /// The control needs the task (<see cref="Host"/>, usually <c>{Binding}</c>) and the slot of the task (<see cref="SlotId"/>);
    /// without a task that can be edited it is only a frame around the box.
    /// </para>
    /// </summary>
    [ContentProperty(nameof(EmbeddedKey))]
    public partial class KeySlotEditor : UserControl
    {
        /// <summary>Identifies <see cref="SlotId"/>.</summary>
        public static readonly DependencyProperty SlotIdProperty = DependencyProperty.Register(
            nameof(SlotId), typeof(string), typeof(KeySlotEditor), new PropertyMetadata(null, OnSourceChanged));

        /// <summary>Identifies <see cref="Host"/>.</summary>
        public static readonly DependencyProperty HostProperty = DependencyProperty.Register(
            nameof(Host), typeof(object), typeof(KeySlotEditor), new PropertyMetadata(null, OnSourceChanged));

        /// <summary>Identifies <see cref="EmbeddedKey"/>.</summary>
        public static readonly DependencyProperty EmbeddedKeyProperty = DependencyProperty.Register(
            nameof(EmbeddedKey), typeof(UIElement), typeof(KeySlotEditor), new PropertyMetadata(null, OnEmbeddedKeyChanged));

        /// <summary>Identifies <see cref="State"/>.</summary>
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State), typeof(object), typeof(KeySlotEditor), new PropertyMetadata(null));

        /// <summary>Identifies <see cref="EmbeddedMargin"/>.</summary>
        public static readonly DependencyProperty EmbeddedMarginProperty = DependencyProperty.Register(
            nameof(EmbeddedMargin), typeof(Thickness), typeof(KeySlotEditor), new PropertyMetadata(new Thickness(0)));

        /// <summary>Identifies <see cref="SwitchMargin"/>.</summary>
        public static readonly DependencyProperty SwitchMarginProperty = DependencyProperty.Register(
            nameof(SwitchMargin), typeof(Thickness), typeof(KeySlotEditor), new PropertyMetadata(new Thickness(0)));

        /// <summary>Identifies <see cref="EmbeddedHeight"/>.</summary>
        public static readonly DependencyProperty EmbeddedHeightProperty = DependencyProperty.Register(
            nameof(EmbeddedHeight), typeof(double), typeof(KeySlotEditor), new PropertyMetadata(double.NaN));

        /// <summary>Creates the control.</summary>
        public KeySlotEditor()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                if (State == null)
                {
                    Rebuild();
                }
            };

            // A tab that is left lets go of the task, so that the state does not keep listening to it.
            Unloaded += (_, _) => DisposeState();
        }

        /// <summary>Gets or sets the slot of the task that this editor edits; see the ids in <c>SecureKeySlots</c>.</summary>
        public string SlotId
        {
            get => (string)GetValue(SlotIdProperty);
            set => SetValue(SlotIdProperty, value);
        }

        /// <summary>Gets or sets the task that is edited: a setup view model of a DESFire or MIFARE Classic task.</summary>
        public object Host
        {
            get => GetValue(HostProperty);
            set => SetValue(HostProperty, value);
        }

        /// <summary>Gets or sets the box in which the key is typed; it is shown while the slot has no reference.</summary>
        public UIElement EmbeddedKey
        {
            get => (UIElement)GetValue(EmbeddedKeyProperty);
            set => SetValue(EmbeddedKeyProperty, value);
        }

        /// <summary>
        /// Gets what the editor shows and does for the slot; <see langword="null"/> without an editable task. It is an object
        /// so that the window can bind to it without making the type public.
        /// </summary>
        public object State
        {
            get => GetValue(StateProperty);
            private set => SetValue(StateProperty, value);
        }

        /// <summary>Gets the margin of the wrapped box, which the box for the reference takes over.</summary>
        public Thickness EmbeddedMargin
        {
            get => (Thickness)GetValue(EmbeddedMarginProperty);
            private set => SetValue(EmbeddedMarginProperty, value);
        }

        /// <summary>Gets the margin of the button that switches the mode.</summary>
        public Thickness SwitchMargin
        {
            get => (Thickness)GetValue(SwitchMarginProperty);
            private set => SetValue(SwitchMarginProperty, value);
        }

        /// <summary>Gets the height of the wrapped box, which the box for the reference and the button take over; NaN is as high as needed.</summary>
        public double EmbeddedHeight
        {
            get => (double)GetValue(EmbeddedHeightProperty);
            private set => SetValue(EmbeddedHeightProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((KeySlotEditor)d).Rebuild();

        private static void OnEmbeddedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (KeySlotEditor)d;
            editor.EmbeddedHost.Child = e.NewValue as UIElement;

            if (e.NewValue is FrameworkElement box)
            {
                editor.EmbeddedMargin = box.Margin;
                editor.EmbeddedHeight = box.Height;
                editor.SwitchMargin = new Thickness(0, box.Margin.Top, box.Margin.Right, box.Margin.Bottom);
            }
        }

        private void Rebuild()
        {
            DisposeState();

            if (Host is IKeySlotHost host && !string.IsNullOrEmpty(SlotId))
            {
                State = new KeySlotEditorState(
                    host,
                    SlotId,
                    KeyResolutionScope.Current?.Registry.Providers ?? (IReadOnlyList<SecureKeyProviderInfo>)Array.Empty<SecureKeyProviderInfo>(),
                    new WpfKeySlotEditorDialogs(() => Window.GetWindow(this)),
                    new WindowsCredentialKeyProvider(new NativeCredentialStore()));
            }
        }

        private void DisposeState()
        {
            (State as IDisposable)?.Dispose();
            State = null;
        }
    }
}
