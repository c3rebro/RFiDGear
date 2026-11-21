#nullable enable
using System;

namespace RFiDGear.Services.AutoMode
{
    /// <summary>
    /// The card that a run read for itself is not the card that was approved: it was replaced between the approval and the start of the
    /// run. It is thrown before the first task, so nothing was written to the card; the run ends as a fault, which stops automatic mode.
    /// </summary>
    internal sealed class ApprovedCardChangedException : InvalidOperationException
    {
        /// <summary>Initializes the exception with its fixed text (no UID is put into it).</summary>
        internal ApprovedCardChangedException()
            : base("The card on the reader is not the card that was approved.")
        {
        }
    }
}
