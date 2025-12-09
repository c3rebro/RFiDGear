using System;

namespace Elatec.NET
{
    /// <summary>
    /// Exception that wraps an error code returned by GetLastErrorAsync.
    /// </summary>
    public class ReaderException : Exception
    {
        public ReaderError ErrorNumber { get; internal set; }

        public ReaderException()
        {
        }

        public ReaderException(string message) : base(message)
        {
        }

        public ReaderException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ReaderException(string message, ReaderError errorNumber) : base(message)
        {
            ErrorNumber = errorNumber;
        }

        public ReaderException(string message, ReaderError errorNumber, Exception innerException) : base(message, innerException)
        {
            ErrorNumber = errorNumber;
        }

    }
}
