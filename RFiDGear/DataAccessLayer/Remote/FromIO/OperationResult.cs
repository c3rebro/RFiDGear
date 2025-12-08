using System.Collections.Generic;
using System.Collections.ObjectModel;
using RFiDGear.DataAccessLayer.Tasks;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public class OperationResult
    {
        public ERROR Code { get; }

        public string Message { get; }

        public string Details { get; }

        public string Operation { get; }

        public bool WasAuthenticated { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public bool IsSuccess => Code == ERROR.NoError;

        private OperationResult(
            ERROR code,
            string message,
            string details,
            string operation,
            bool wasAuthenticated,
            IDictionary<string, string> metadata)
        {
            Code = code;
            Message = message;
            Details = details;
            Operation = operation;
            WasAuthenticated = wasAuthenticated;
            Metadata = new ReadOnlyDictionary<string, string>(metadata ?? new Dictionary<string, string>());
        }

        public static OperationResult Success(
            string message = "",
            string operation = "",
            bool wasAuthenticated = false,
            IDictionary<string, string> metadata = null)
        {
            return new OperationResult(ERROR.NoError, message, string.Empty, operation, wasAuthenticated, metadata);
        }

        public static OperationResult Failure(
            ERROR code,
            string message,
            string details = "",
            string operation = "",
            bool wasAuthenticated = false,
            IDictionary<string, string> metadata = null)
        {
            return new OperationResult(code, message, details, operation, wasAuthenticated, metadata);
        }
    }
}
