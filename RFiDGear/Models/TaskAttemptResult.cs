using System;
using RFiDGear.Infrastructure;

namespace RFiDGear.Models
{
    /// <summary>
    /// Captures the outcome of a single task execution attempt.
    /// </summary>
    public class TaskAttemptResult
    {
        public DateTimeOffset AttemptedAt { get; set; }

        public ERROR ErrorLevel { get; set; }

        public bool? WasSuccessful { get; set; }

        public string Message { get; set; }
    }
}
