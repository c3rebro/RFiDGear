using System.ComponentModel;

namespace RFiDGear.Extensions.CardCheckAssistant.Localization
{
    /// <summary>
    /// The binding source of the page's texts: <c>{Binding Strings[Key]}</c>. <see cref="Refresh"/> changes the language
    /// and tells every bound text to read itself again.
    /// </summary>
    public sealed class LocalizedStrings : INotifyPropertyChanged
    {
        private string _cultureName;

        /// <param name="cultureName">The host's <c>IAppContext.UICultureName</c>.</param>
        public LocalizedStrings(string? cultureName)
        {
            _cultureName = cultureName ?? string.Empty;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>The text for a key in the current language.</summary>
        /// <param name="key">One of <c>ExtensionStrings.Keys</c>.</param>
        public string this[string key] => ExtensionStrings.Get(key, _cultureName);

        /// <summary>Switches the language and raises the change for every indexer binding.</summary>
        /// <param name="cultureName">The new <c>IAppContext.UICultureName</c>.</param>
        public void Refresh(string? cultureName)
        {
            _cultureName = cultureName ?? string.Empty;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        /// <summary>The text for a key with its placeholders filled in.</summary>
        /// <param name="key">One of <c>ExtensionStrings.Keys</c>.</param>
        /// <param name="arguments">The values for <c>{0}</c>, <c>{1}</c>...</param>
        public string Format(string key, params object[] arguments) =>
            string.Format(System.Globalization.CultureInfo.CurrentCulture, this[key], arguments);
    }
}
