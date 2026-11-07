namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
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
