/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 15.11.2017
 * Time: 21:55
 * 
 * Based on the article: Patterns for Asynchronous MVVM Applications: Commands
 * http://msdn.microsoft.com/en-us/magazine/dn630647.aspx
 * 
 * Modifed by Scott Chamberlain on 12/03/2014
 * Split in to two classes, one that does not return a result and a 
 * derived class that does.
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RFiDGear.DataAccessLayer
{
    /// <summary>
    /// Wraps a task with a result and exposes completion state via properties.
    /// </summary>
    public sealed class NotifyTaskCompletion<TResult> : NotifyTaskCompletion
    {
        public NotifyTaskCompletion(Task<TResult> task)
            : base(task)
        {
        }

        /// <summary>
        /// Gets the task result when the task completes successfully; otherwise default.
        /// </summary>
        public TResult Result
        {
            get
            {
                return (Task.Status == TaskStatus.RanToCompletion) ?
                    ((Task<TResult>)Task).Result : default(TResult);
            }
        }
    }

    /// <summary>
    /// Wraps a task and exposes completion state changes via <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public class NotifyTaskCompletion : INotifyPropertyChanged
    {
        public NotifyTaskCompletion(Task task)
        {
            Task = task;
            if (!task.IsCompleted)
                TaskCompletion = WatchTaskAsync(task);
            else
                TaskCompletion = Task;
        }

        private async Task WatchTaskAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Trace.TraceError("NotifyTaskCompletion observed task exception: {0}", ex);
            }
            var propertyChanged = PropertyChanged;
            if (propertyChanged == null)
                return;
            propertyChanged(this, new PropertyChangedEventArgs("Status"));
            propertyChanged(this, new PropertyChangedEventArgs("IsCompleted"));
            propertyChanged(this, new PropertyChangedEventArgs("IsNotCompleted"));
            if (task.IsCanceled)
            {
                propertyChanged(this, new PropertyChangedEventArgs("IsCanceled"));
            }
            else if (task.IsFaulted)
            {
                propertyChanged(this, new PropertyChangedEventArgs("IsFaulted"));
                propertyChanged(this, new PropertyChangedEventArgs("Exception"));
                propertyChanged(this, new PropertyChangedEventArgs("InnerException"));
                propertyChanged(this, new PropertyChangedEventArgs("ErrorMessage"));
            }
            else
            {
                propertyChanged(this, new PropertyChangedEventArgs("IsSuccessfullyCompleted"));
                propertyChanged(this, new PropertyChangedEventArgs("Result"));
            }
        }

        /// <summary>
        /// Gets the underlying task being tracked.
        /// </summary>
        public Task Task { get; private set; }

        /// <summary>
        /// Gets the task that completes when the tracked task finishes.
        /// </summary>
        public Task TaskCompletion { get; private set; }

        /// <summary>
        /// Gets the current status of the tracked task.
        /// </summary>
        public TaskStatus Status { get { return Task.Status; } }

        /// <summary>
        /// Gets whether the tracked task has completed.
        /// </summary>
        public bool IsCompleted { get { return Task.IsCompleted; } }

        /// <summary>
        /// Gets whether the tracked task is still running.
        /// </summary>
        public bool IsNotCompleted { get { return !Task.IsCompleted; } }
        /// <summary>
        /// Gets whether the tracked task completed successfully.
        /// </summary>
        public bool IsSuccessfullyCompleted
        {
            get
            {
                return Task.Status ==
                    TaskStatus.RanToCompletion;
            }
        }
        /// <summary>
        /// Gets whether the tracked task was canceled.
        /// </summary>
        public bool IsCanceled { get { return Task.IsCanceled; } }

        /// <summary>
        /// Gets whether the tracked task faulted.
        /// </summary>
        public bool IsFaulted { get { return Task.IsFaulted; } }

        /// <summary>
        /// Gets the aggregate exception raised by the tracked task, if any.
        /// </summary>
        public AggregateException Exception { get { return Task.Exception; } }

        /// <summary>
        /// Gets the inner exception for convenience, if available.
        /// </summary>
        public Exception InnerException
        {
            get
            {
                return (Exception == null) ?
                    null : Exception.InnerException;
            }
        }
        /// <summary>
        /// Gets the inner exception message, if available.
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                return (InnerException == null) ?
                    null : InnerException.Message;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
