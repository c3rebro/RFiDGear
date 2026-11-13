using System;
using System.Threading.Tasks;

using Serilog;

namespace RFiDGear.Services
{
    /// <summary>
    /// The notice that the software must be restarted for a setting to apply ends the process when it is accepted. If the
    /// settings are still being written at that moment the file is cut off, so the notice is offered only once the write has
    /// finished, and not at all when it failed.
    /// </summary>
    internal static class RestartNotice
    {
        /// <summary>Waits for the settings to be saved, then offers the notice.</summary>
        /// <param name="save">The write of the settings.</param>
        /// <param name="offer">Offers the notice; called on the context that called this method, after the write succeeded.</param>
        /// <returns>A task that completes when the notice has been offered or the write has failed. It does not throw for a failed write.</returns>
        internal static async Task OfferWhenSavedAsync(Task save, Action offer)
        {
            ArgumentNullException.ThrowIfNull(save);
            ArgumentNullException.ThrowIfNull(offer);

            try
            {
                await save;
            }
            catch (Exception e)
            {
                // Nothing to restart for: the setting did not reach the file. The log has the type only.
                Log.ForContext(typeof(RestartNotice)).Warning("The settings could not be saved, so no restart is offered ({ExceptionType})", e.GetType().FullName);

                return;
            }

            offer();
        }
    }
}
