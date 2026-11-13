using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// A frame for one line of text that takes the width it is given and never asks for any. A column that is as wide as its
    /// content (the data explorer has such) would otherwise grow with a long name; here the text is cut short with an ellipsis
    /// instead. Its height is the height of the line.
    /// </summary>
    internal sealed class LineWithoutWidth : Decorator
    {
        /// <inheritdoc />
        protected override Size MeasureOverride(Size constraint)
        {
            if (Child == null)
            {
                return new Size(0, 0);
            }

            Child.Measure(constraint);

            return new Size(0, Child.DesiredSize.Height);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));

            return arrangeSize;
        }
    }

    /// <summary>
    /// Wraps the box in which a key is typed. While the slot is named by a reference to a key in a key store (which the task looks
    /// up when it runs) the box is greyed out and a line below it says which key store and which name; the choice itself is made
    /// in the key manager, which a button at the bottom of the section opens. With a typed key the control is only a frame around
    /// the box and has no height of its own.
    /// <para>
    /// The control needs the task (<see cref="Host"/>, usually <c>{Binding}</c>) and the slot of the task (<see cref="SlotId"/>);
    /// without a task that can be edited it is only a frame around the box.
    /// </para>
    /// </summary>
    [ContentProperty(nameof(EmbeddedKey))]
    [SuppressMessage("Minor Code Smell", "S2333:Partial is gratuitous in this context", Justification = "The other half of the class is generated from the XAML, which the analyser cannot see.")]
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

        /// <summary>Identifies <see cref="CaptionMargin"/>.</summary>
        public static readonly DependencyProperty CaptionMarginProperty = DependencyProperty.Register(
            nameof(CaptionMargin), typeof(Thickness), typeof(KeySlotEditor), new PropertyMetadata(new Thickness(0)));

        /// <summary>Identifies <see cref="CoverMargin"/>.</summary>
        public static readonly DependencyProperty CoverMarginProperty = DependencyProperty.Register(
            nameof(CoverMargin), typeof(Thickness), typeof(KeySlotEditor), new PropertyMetadata(new Thickness(0)));

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

        /// <summary>Gets or sets the box in which the key is typed; it is greyed out while the slot has a reference.</summary>
        public UIElement EmbeddedKey
        {
            get => (UIElement)GetValue(EmbeddedKeyProperty);
            set => SetValue(EmbeddedKeyProperty, value);
        }

        /// <summary>
        /// Gets what the editor shows for the slot; <see langword="null"/> without an editable task. It is an object so that the
        /// window can bind to it without making the type public.
        /// </summary>
        public object State
        {
            get => GetValue(StateProperty);
            private set => SetValue(StateProperty, value);
        }

        /// <summary>
        /// Gets the margin of the line below the key box: it starts under the text of the box and ends where the box ends, and it
        /// moves up into the margin below the box so that it belongs to the box.
        /// </summary>
        public Thickness CaptionMargin
        {
            get => (Thickness)GetValue(CaptionMarginProperty);
            private set => SetValue(CaptionMarginProperty, value);
        }

        /// <summary>Gets the margin of the box that the key box's own margin gives it, which the cover over it takes over.</summary>
        public Thickness CoverMargin
        {
            get => (Thickness)GetValue(CoverMarginProperty);
            private set => SetValue(CoverMarginProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((KeySlotEditor)d).Rebuild();

        private static void OnEmbeddedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (KeySlotEditor)d;
            editor.EmbeddedHost.Child = e.NewValue as UIElement;

            if (e.NewValue is FrameworkElement box)
            {
                editor.CoverMargin = box.Margin;
                editor.CaptionMargin = new Thickness(box.Margin.Left + 2, -Math.Min(box.Margin.Bottom, 4), box.Margin.Right, box.Margin.Bottom);
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
