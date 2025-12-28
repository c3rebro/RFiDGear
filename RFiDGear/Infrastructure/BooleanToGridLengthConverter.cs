using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Converts a boolean into a <see cref="GridLength"/> based on the provided parameter.
    /// </summary>
    public sealed class BooleanToGridLengthConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value into a <see cref="GridLength"/>.
        /// </summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to determine the visible width.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A <see cref="GridLength"/> instance.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isVisible || !isVisible)
            {
                return new GridLength(0);
            }

            if (parameter is string widthParameter && !string.IsNullOrWhiteSpace(widthParameter))
            {
                if (string.Equals(widthParameter, "*", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(widthParameter, "star", StringComparison.OrdinalIgnoreCase))
                {
                    return new GridLength(1, GridUnitType.Star);
                }

                if (double.TryParse(widthParameter, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels))
                {
                    return new GridLength(pixels);
                }
            }

            return new GridLength(1, GridUnitType.Star);
        }

        /// <summary>
        /// Converts a <see cref="GridLength"/> back to a boolean value.
        /// </summary>
        /// <param name="value">The value produced by the binding target.</param>
        /// <param name="targetType">The type of the binding source property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>Throws <see cref="NotSupportedException"/>.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("BooleanToGridLengthConverter does not support ConvertBack.");
        }
    }
}
