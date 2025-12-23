using System;
using System.Windows;

namespace RFiDGear.Infrastructure
{
    /// <summary>
    /// Provides a safe way to execute UI-bound updates from background threads.
    /// </summary>
    public static class UiDispatcher
    {
        /// <summary>
        /// Invokes the action on the UI dispatcher when needed, or inline when already on the UI thread.
        /// </summary>
        /// <param name="action">The work to execute.</param>
        public static void InvokeIfRequired(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}
