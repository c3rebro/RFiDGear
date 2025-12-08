using RFiDGear.DataAccessLayer.Tasks;

namespace RFiDGear.DataAccessLayer.Remote.FromIO
{
    public class OperationResult
    {
        public ERROR Code { get; }

        public string Message { get; }

        public string Details { get; }

        public bool IsSuccess => Code == ERROR.NoError;

        private OperationResult(ERROR code, string message, string details)
        {
            Code = code;
            Message = message;
            Details = details;
        }

        public static OperationResult Success(string message = "")
        {
            return new OperationResult(ERROR.NoError, message, string.Empty);
        }

        public static OperationResult Failure(ERROR code, string message, string details = "")
        {
            return new OperationResult(code, message, details);
        }
    }
}
