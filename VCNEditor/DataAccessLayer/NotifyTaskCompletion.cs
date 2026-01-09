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
    public sealed class NotifyTaskCompletion<TResult> : NotifyTaskCompletion
    {
        public NotifyTaskCompletion(Task<TResult> task)
            : base(task)
        {
        }

        public TResult Result
        {
            get
            {
                return (Task.Status == TaskStatus.RanToCompletion) ?
                    ((Task<TResult>)Task).Result : default(TResult);
            }
        }
    }

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

        public Task Task { get; private set; }
        public Task TaskCompletion { get; private set; }
        public TaskStatus Status { get { return Task.Status; } }
        public bool IsCompleted { get { return Task.IsCompleted; } }
        public bool IsNotCompleted { get { return !Task.IsCompleted; } }
        public bool IsSuccessfullyCompleted
        {
            get
            {
                return Task.Status ==
                    TaskStatus.RanToCompletion;
            }
        }
        public bool IsCanceled { get { return Task.IsCanceled; } }
        public bool IsFaulted { get { return Task.IsFaulted; } }
        public AggregateException Exception { get { return Task.Exception; } }
        public Exception InnerException
        {
            get
            {
                return (Exception == null) ?
                    null : Exception.InnerException;
            }
        }
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
