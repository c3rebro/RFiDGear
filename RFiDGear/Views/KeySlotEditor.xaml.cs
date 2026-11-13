using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using RFiDGear.Services.SecureKeys;

namespace RFiDGear.View
{
    /// <summary>
    /// Wraps the box in which a key is typed. While the slot is named by a reference to a key in a key store (which the task looks
    /// up when it runs) the box is greyed out and shows the reference instead of the key: the line that says which key it is, with
    /// the key store in its tooltip. The choice itself is made in the key manager, which a button at the bottom of the section
    /// opens. With a typed key the control is only a frame around the box, and the box is what it always was.
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
            nameof(State), typeof(object), typeof(KeySlotEditor), new PropertyMetadata(null, OnStateChanged));

        // What the box had before it showed the reference, so that it gets it back: the box is the view's own, and what it was set up
        // with (its binding to the key of the task, its colour, its tooltip) is not ours to lose.
        private List<SavedValue> _saved;
        private TextBox _referenceBox;

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

        /// <summary>Gets or sets the box in which the key is typed; it is greyed out and shows the reference while the slot has one.</summary>
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

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((KeySlotEditor)d).Rebuild();

        private static void OnEmbeddedKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (KeySlotEditor)d;

            // The old box gets back what it had before the new one is put in its place.
            editor.ShowKey();
            editor.EmbeddedHost.Child = e.NewValue as UIElement;
            editor.ApplyMode();
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (KeySlotEditor)d;

            if (e.OldValue is INotifyPropertyChanged old)
            {
                old.PropertyChanged -= editor.OnStatePropertyChanged;
            }

            if (e.NewValue is INotifyPropertyChanged state)
            {
                state.PropertyChanged += editor.OnStatePropertyChanged;
            }

            editor.ApplyMode();
        }

        private void OnStatePropertyChanged(object sender, PropertyChangedEventArgs e) => ApplyMode();

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

        // The box shows the reference while the slot has one, and the key of the task otherwise.
        private void ApplyMode()
        {
            var bound = State is KeySlotEditorState { IsReferenceMode: true };

            if (bound && _referenceBox == null && EmbeddedKey is TextBox box)
            {
                ShowReference(box);
            }
            else if (!bound)
            {
                ShowKey();
            }
        }

        private void ShowReference(TextBox box)
        {
            _referenceBox = box;
            _saved = new List<SavedValue>
            {
                new SavedValue(box, TextBox.TextProperty),
                new SavedValue(box, Control.BackgroundProperty),
                new SavedValue(box, FrameworkElement.ToolTipProperty),
            };

            // The text of the box is the line that says which key it is. The binding to the key of the task is gone meanwhile, so
            // nothing typed or shown here can reach it, and the key of the task is what the binding to the key store made it: empty.
            box.SetBinding(TextBox.TextProperty, new Binding("State.ReferenceCaption") { Source = this, Mode = BindingMode.OneWay });

            // An empty key is invalid, which the box colours red; a box that shows a reference is not invalid, only not for typing.
            box.Background = SystemColors.ControlBrush;

            var tip = new ToolTip { Style = (Style)FindResource("KeySlotToolTipStyle") };
            tip.SetBinding(ContentControl.ContentProperty, new Binding("State.ReferenceDetail") { Source = this, Mode = BindingMode.OneWay });
            box.ToolTip = tip;
        }

        private void ShowKey()
        {
            if (_referenceBox == null)
            {
                return;
            }

            foreach (var saved in _saved)
            {
                saved.Restore();
            }

            _saved = null;
            _referenceBox = null;
        }

        /// <summary>A property of the box the way it was set before the editor changed it: a binding, a value, or nothing.</summary>
        private sealed class SavedValue
        {
            private readonly DependencyObject _target;
            private readonly DependencyProperty _property;
            private readonly BindingBase _binding;
            private readonly object _local;

            internal SavedValue(DependencyObject target, DependencyProperty property)
            {
                _target = target;
                _property = property;
                _binding = BindingOperations.GetBindingBase(target, property);
                _local = _binding == null ? target.ReadLocalValue(property) : DependencyProperty.UnsetValue;
            }

            internal void Restore()
            {
                if (_binding != null)
                {
                    BindingOperations.SetBinding(_target, _property, _binding);
                }
                else if (ReferenceEquals(_local, DependencyProperty.UnsetValue))
                {
                    _target.ClearValue(_property);
                }
                else
                {
                    _target.SetValue(_property, _local);
                }
            }
        }
    }
}
