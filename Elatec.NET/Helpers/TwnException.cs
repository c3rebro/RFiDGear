using System;

namespace Elatec.NET
{
    /// <summary>
    /// Exception that wraps an error code returned in the first byte of the response to a Simple Protocol command.
    /// </summary>
    public class TwnException : Exception
    {
        public ResponseError ErrorNumber { get; internal set; }

        public TwnException()
        {
        }

        public TwnException(string message) : base(message)
        {
        }

        public TwnException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public TwnException(string message, ResponseError errorNumber) : base(message)
        {
            ErrorNumber = errorNumber;
        }

        public TwnException(string message, ResponseError errorNumber, Exception innerException) : base(message, innerException)
        {
            ErrorNumber = errorNumber;
        }

    }
}
