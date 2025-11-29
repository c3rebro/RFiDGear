using System;
using System.Windows;
using System.Windows.Input;

using Serilog;

namespace RFiDGear.Behaviors
{
    public static class WindowDragBehavior
    {
        public static readonly DependencyProperty EnableWindowDragProperty = DependencyProperty.RegisterAttached(
            "EnableWindowDrag",
            typeof(bool),
            typeof(WindowDragBehavior),
            new PropertyMetadata(false, OnEnableWindowDragChanged));

        private static readonly ILogger Logger = Log.ForContext(typeof(WindowDragBehavior));

        public static bool GetEnableWindowDrag(DependencyObject target)
        {
            return (bool)target.GetValue(EnableWindowDragProperty);
        }

        public static void SetEnableWindowDrag(DependencyObject target, bool value)
        {
            target.SetValue(EnableWindowDragProperty, value);
        }

        private static void OnEnableWindowDragChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not Window window)
            {
                return;
            }

            window.MouseLeftButtonDown -= OnMouseLeftButtonDown;

            if (e.NewValue is bool enable && enable)
            {
                window.MouseLeftButtonDown += OnMouseLeftButtonDown;
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Window window)
            {
                return;
            }

            try
            {
                window.DragMove();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Window drag move failed but was ignored to keep UI responsive");
            }
        }
    }
}
