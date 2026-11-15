#nullable enable
using Elatec.NET.Sam;

namespace RFiDGear.Services.Sam
{
    /// <summary>
    /// The reader failed to carry a command to the SAM or its answer back, as opposed to a SAM that answered and said no
    /// (a plain <see cref="SamException"/> with a status word). After this the state of the SAM is unknown: the command may
    /// have been executed. The text is fixed; the text of the reader library is not passed on.
    /// </summary>
    internal sealed class SamReaderException : SamException
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="message">A fixed text; never the message of another exception.</param>
        internal SamReaderException(string message)
            : base(message)
        {
        }
    }
}