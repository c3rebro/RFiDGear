using System.Collections.Generic;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// Represents the outcome of a reader operation with contextual information.
    /// </summary>
    public class OperationResult
    {
        public ERROR Code { get; set; }

        public string Message { get; set; }

        public string Details { get; set; }

        public string Operation { get; set; }

        public bool WasAuthenticated { get; set; }

        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        public static OperationResult Success(
            ERROR code = ERROR.NoError,
            string message = null,
            string details = null,
            string operation = null,
            bool wasAuthenticated = true,
            IDictionary<string, string> metadata = null)
        {
            return new OperationResult
            {
                Code = code,
                Message = message,
                Details = details,
                Operation = operation,
                WasAuthenticated = wasAuthenticated,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
        }

        public static OperationResult Failure(
            ERROR code,
            string message = null,
            string details = null,
            string operation = null,
            bool wasAuthenticated = false,
            IDictionary<string, string> metadata = null)
        {
            return new OperationResult
            {
                Code = code,
                Message = message,
                Details = details,
                Operation = operation,
                WasAuthenticated = wasAuthenticated,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
        }
    }
}
