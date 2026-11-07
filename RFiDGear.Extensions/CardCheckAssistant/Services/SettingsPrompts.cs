using RFiDGear.Extensions.CardCheckAssistant.Localization;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Opens the settings window for the page. A seam, so the page's view model is tested without a window on the screen.
    /// </summary>
    internal interface ISettingsDialog
    {
        /// <summary>
        /// Shows the settings window and waits until it is closed. Called on the UI thread; the window is modal to the one
        /// the operator is working in.
        /// </summary>
        /// <param name="strings">The page's texts; the window shares them, so it follows a change of language.</param>
        /// <returns><c>true</c> if the settings were saved, so the page should read what it starts from again.</returns>
        bool Show(LocalizedStrings strings);
    }

    /// <summary>
    /// The one question the settings window may have to ask the operator. A seam, so the view model is tested without a
    /// message box on the screen. It is called on the UI thread.
    /// </summary>
    internal interface ISettingsPrompts
    {
        /// <summary>Asks whether changes that were not saved may be thrown away.</summary>
        /// <param name="title">The title of the question, in the page's language.</param>
        /// <param name="message">The question, in the page's language.</param>
        /// <returns><c>true</c> if the changes may be discarded.</returns>
        bool ConfirmDiscard(string title, string message);
    }
}
