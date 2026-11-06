using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RFiDGear.Extensions.CardCheckAssistant.Mvvm
{
    /// <summary>
    /// The smallest base class a view model needs: change notification, with the equality check in one place. The
    /// extension keeps its own so it does not have to carry a toolkit next to the host's.
    /// </summary>
    internal abstract class ObservableBase : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Raises <see cref="PropertyChanged"/> for one property.</summary>
        /// <param name="propertyName">The property; filled in by the compiler from the calling member.</param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Stores a value and raises <see cref="PropertyChanged"/> if it differs from the current one.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The property; filled in by the compiler from the calling member.</param>
        /// <returns><c>true</c> if the value changed.</returns>
        internal bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
