using System.Windows;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Services;

using static RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings;

namespace RFiDGear.Extensions.CardCheckAssistant.Views
{
    /// <summary>
    /// What the window shows when the extension's support library is not there or does not fit: one wrapped paragraph
    /// that names the two files and the folder they belong in. It has no state and nothing to dispose.
    /// </summary>
    internal static class MissingComponentPage
    {
        /// <summary>Builds the page.</summary>
        /// <param name="cultureName">The host's <c>IAppContext.UICultureName</c>; blank selects English.</param>
        /// <returns>A new page; the window takes it over.</returns>
        internal static FrameworkElement Create(string? cultureName)
        {
            var extensionFile = typeof(MissingComponentPage).Assembly.GetName().Name + ".dll";

            return new TextBlock
            {
                Text = new LocalizedStrings(cultureName).Format(Keys.ComponentMissing, ComponentCheck.LibraryFileName, extensionFile),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
            };
        }
    }
}
