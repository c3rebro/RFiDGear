using System;

namespace RedCell.Diagnostics.Update
{
    /// <summary>
    /// Class LogEventArgs.
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        #region Initialization
        /// <summary>
        /// Initializes a new instance of the <see cref="LogEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public LogEventArgs (string message)
        {
            Message = message;
            TimeStamp = DateTime.Now;
        }
        #endregion

        #region Proprties
        /// <summary>
        /// Gets the time stamp.
        /// </summary>
        /// <value>The time stamp.</value>
        public DateTime TimeStamp { get; private set; }

        /// <summary>
        /// Gets the message.
        /// </summary>
        /// <value>The message.</value>
        public string Message { get; private set; }
        #endregion
    }
}
