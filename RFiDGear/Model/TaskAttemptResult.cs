using System;
using RFiDGear.DataAccessLayer;

namespace RFiDGear.Model
{
    public class TaskAttemptResult
    {
        public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.Now;

        public ERROR ErrorLevel { get; set; }

        public bool? WasSuccessful { get; set; }

        public string Message { get; set; }
    }
}
