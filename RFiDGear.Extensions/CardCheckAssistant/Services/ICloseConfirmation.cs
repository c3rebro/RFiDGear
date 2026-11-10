namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// The question that is asked when the window is closed while a check is in progress but nothing is running that
    /// cannot be left. The page supplies the texts, already in the language of the page; the answer decides whether the
    /// window closes.
    /// </summary>
    internal interface ICloseConfirmation
    {
        /// <summary>Asks whether the window may be closed.</summary>
        /// <param name="title">The title of the question.</param>
        /// <param name="text">What closing means for the order in its present state.</param>
        /// <returns><c>true</c> if the operator wants to close the window.</returns>
        bool Confirm(string title, string text);
    }
}
