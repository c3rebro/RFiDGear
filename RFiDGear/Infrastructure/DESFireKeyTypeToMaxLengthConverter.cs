using RFiDGear.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Converts a <see cref="DESFireKeyType"/> value to the maximum permitted hex-character count
    /// for a key TextBox (48 for <see cref="DESFireKeyType.DF_KEY_3K3DES"/>, 32 for all other types).
    /// </summary>
    [ValueConversion(typeof(DESFireKeyType), typeof(int))]
    public sealed class DESFireKeyTypeToMaxLengthConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is DESFireKeyType keyType ? CustomConverter.GetExpectedKeyHexLength(keyType) : 32;

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException($"{nameof(DESFireKeyTypeToMaxLengthConverter)} does not support ConvertBack.");
    }
}
