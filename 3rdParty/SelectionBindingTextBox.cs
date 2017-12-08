/*
 * Created by SharpDevelop.
 * Date: 11/29/2017
 * Time: 15:59
 * 
 */
using System;
using System.Windows.Controls;
using System.Windows;

namespace RFiDGear._3rdParty
{
	/// <summary>
	/// Description of SelectionBindingTextBox.
	/// </summary>
	public class SelectionBindingTextBox : TextBox
	{
		
		public static readonly DependencyProperty BindableFocusProperty =
			DependencyProperty.RegisterAttached(
				"IsFocused",
				typeof(bool),
				typeof(SelectionBindingTextBox),
				new UIPropertyMetadata(false, OnIsFocusedPropertyChanged));
		
		public static readonly DependencyProperty BindableSelectionStartProperty =
			DependencyProperty.Register(
				"BindableSelectionStart",
				typeof(int),
				typeof(SelectionBindingTextBox),
				new PropertyMetadata(OnBindableSelectionStartChanged));

		public static readonly DependencyProperty BindableSelectionLengthProperty =
			DependencyProperty.Register(
				"BindableSelectionLength",
				typeof(int),
				typeof(SelectionBindingTextBox),
				new PropertyMetadata(OnBindableSelectionLengthChanged));

		private bool changeFromUI;

		public SelectionBindingTextBox() : base()
		{
			this.SelectionChanged += this.OnSelectionChanged;
		}

		public static bool GetIsFocused(DependencyObject obj)
		{
			return (bool) obj.GetValue(IsFocusedProperty);
		}

		public static void SetIsFocused(DependencyObject obj, bool value)
		{
			obj.SetValue(IsFocusedProperty, value);
		}
		
		public int BindableSelectionStart
		{
			get
			{
				return (int)this.GetValue(BindableSelectionStartProperty);
			}

			set
			{
				this.SetValue(BindableSelectionStartProperty, value);
			}
		}

		public int BindableSelectionLength
		{
			get
			{
				return (int)this.GetValue(BindableSelectionLengthProperty);
			}

			set
			{
				this.SetValue(BindableSelectionLengthProperty, value);
			}
		}

		private static void OnBindableSelectionStartChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var textBox = dependencyObject as SelectionBindingTextBox;

			if (!textBox.changeFromUI)
			{
				int newValue = (int)args.NewValue;
				textBox.SelectionStart = newValue;
			}
			else
			{
				textBox.changeFromUI = false;
			}
		}

		private static void OnBindableSelectionLengthChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var textBox = dependencyObject as SelectionBindingTextBox;

			if (!textBox.changeFromUI)
			{
				int newValue = (int)args.NewValue;
				textBox.SelectionLength = newValue;
			}
			else
			{
				textBox.changeFromUI = false;
			}
		}

		private void OnSelectionChanged(object sender, RoutedEventArgs e)
		{
			if (this.BindableSelectionStart != this.SelectionStart)
			{
				this.changeFromUI = true;
				this.BindableSelectionStart = this.SelectionStart;
			}

			if (this.BindableSelectionLength != this.SelectionLength)
			{
				this.changeFromUI = true;
				this.BindableSelectionLength = this.SelectionLength;
			}
		}

		private static void OnIsFocusedPropertyChanged(
			DependencyObject d,
			DependencyPropertyChangedEventArgs e)
		{
			var uie = (UIElement) d;
			if ((bool) e.NewValue)
			{
				uie.Focus(); // Don't care about false values.
			}
		}
	}
}
