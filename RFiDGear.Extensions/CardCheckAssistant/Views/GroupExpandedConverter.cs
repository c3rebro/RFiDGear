using System;
using System.Globalization;
using System.Windows.Data;

using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// Tells a group of the order list whether it is open: the page remembers which ones the operator closed, so they stay
    /// closed when the list is read again and its groups are made anew.
    /// </summary>
    internal sealed class GroupExpandedConverter : IMultiValueConverter
    {
        /// <summary>Converts the name of the group and the page to the state of the group.</summary>
        /// <param name="values">The name of the group, and the page.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns><c>true</c> unless the operator closed the group.</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values != null && values.Length > 1 && values[1] is GuidedCheckViewModel page
                ? page.IsGroupExpanded(values[0] as string)
                : true;

        /// <summary>Not supported: the state of a group is written back by the view.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetTypes">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
