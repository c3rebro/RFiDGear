/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 15.11.2017
 * Time: 21:54
 * 
 * Based on the article: Patterns for Asynchronous MVVM Applications: Commands
 * http://msdn.microsoft.com/en-us/magazine/dn630647.aspx
 */
 
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.DataAccessLayer
{
    public abstract class AsyncCommandBase : IAsyncCommand
    {
        public abstract bool CanExecute(object parameter);

        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="parameter">Command parameter.</param>
        public abstract Task ExecuteAsync(object parameter);

        void ICommand.Execute(object parameter)
        {
            SafeFireAndForget(ExecuteAsync(parameter), HandleExecutionException);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        protected void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Handles exceptions raised during asynchronous command execution.
        /// </summary>
        /// <param name="exception">The exception that was raised.</param>
        protected virtual void HandleExecutionException(Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }

        private static void SafeFireAndForget(Task task, Action<Exception> exceptionHandler)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (exceptionHandler == null)
            {
                throw new ArgumentNullException(nameof(exceptionHandler));
            }

            task.ContinueWith(
                continuationTask =>
                {
                    var exception = continuationTask.Exception?.GetBaseException() ?? continuationTask.Exception;
                    if (exception != null)
                    {
                        exceptionHandler(exception);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
