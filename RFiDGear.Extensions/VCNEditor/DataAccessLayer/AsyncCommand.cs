/*
 * Created by SharpDevelop.
 * User: C3rebro
 * Date: 14.11.2017
 * Time: 22:24
 * 
 * Based on the article: Patterns for Asynchronous MVVM Applications: Commands
 * http://msdn.microsoft.com/en-us/magazine/dn630647.aspx
 * 
 * Modified by Scott Chamberlain 11-19-2014
 * - Added parameter support
 * - Added the ability to shut off the single invocation restriction.
 * - Made a non-generic version of the class that called the generic version with a <object> return type.
 */
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RFiDGear.DataAccessLayer
{
	using System;
	using System.ComponentModel;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Input;

	/// <summary>
	/// Executes asynchronous commands with cancellation support and completion tracking.
	/// </summary>
	public class AsyncCommand<TResult> : AsyncCommandBase, INotifyPropertyChanged
	{
		private readonly Func<CancellationToken, Task<TResult>> _command;
		private readonly CancelAsyncCommand _cancelCommand;
		private NotifyTaskCompletion<TResult> _execution;

		public AsyncCommand(Func<CancellationToken, Task<TResult>> command)
		{
			_command = command;
			_cancelCommand = new CancelAsyncCommand();
		}

		public override bool CanExecute(object parameter)
		{
			return Execution == null || Execution.IsCompleted;
		}

		public override async Task ExecuteAsync(object parameter)
		{
			_cancelCommand.NotifyCommandStarting();
			Execution = new NotifyTaskCompletion<TResult>(_command(_cancelCommand.Token));
			RaiseCanExecuteChanged();
			await Execution.TaskCompletion;
			_cancelCommand.NotifyCommandFinished();
			RaiseCanExecuteChanged();
		}

		/// <summary>
		/// Gets a command that requests cancellation for the running task.
		/// </summary>
		public ICommand CancelCommand
		{
			get { return _cancelCommand; }
		}

		/// <summary>
		/// Gets the task execution wrapper that exposes completion status.
		/// </summary>
		public NotifyTaskCompletion<TResult> Execution
		{
			get { return _execution; }
			private set
			{
				_execution = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		private sealed class CancelAsyncCommand : ICommand, IDisposable
		{
			private CancellationTokenSource _cts = new CancellationTokenSource();
			private bool _commandExecuting;

			/// <summary>
			/// Gets the cancellation token for the currently executing command.
			/// </summary>
			public CancellationToken Token { get { return _cts.Token; } }

			public void NotifyCommandStarting()
			{
				_commandExecuting = true;
				if (!_cts.IsCancellationRequested)
				{
					return;
				}

				ResetCancellationTokenSource();
				RaiseCanExecuteChanged();
			}

			public void NotifyCommandFinished()
			{
				_commandExecuting = false;
				RaiseCanExecuteChanged();
			}

			bool ICommand.CanExecute(object parameter)
			{
				return _commandExecuting && !_cts.IsCancellationRequested;
			}

			void ICommand.Execute(object parameter)
			{
				_cts.Cancel();
				RaiseCanExecuteChanged();
			}

			public void Dispose()
			{
				_cts.Dispose();
			}

			public event EventHandler CanExecuteChanged
			{
				add { CommandManager.RequerySuggested += value; }
				remove { CommandManager.RequerySuggested -= value; }
			}

			private void RaiseCanExecuteChanged()
			{
				CommandManager.InvalidateRequerySuggested();
			}

			private void ResetCancellationTokenSource()
			{
				CancellationTokenSource previous = _cts;
				_cts = new CancellationTokenSource();
				previous.Dispose();
			}
		}
	}

	/// <summary>
	/// Factory helpers for creating <see cref="AsyncCommand{TResult}"/> instances.
	/// </summary>
	public static class AsyncCommand
	{
		/// <summary>
		/// Creates a command for a task without a return value.
		/// </summary>
		public static AsyncCommand<object> Create(Func<Task> command)
		{
			return new AsyncCommand<object>(async _ => { await command(); return null; });
		}

		/// <summary>
		/// Creates a command for a task that returns a result.
		/// </summary>
		public static AsyncCommand<TResult> Create<TResult>(Func<Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(_ => command());
		}

		/// <summary>
		/// Creates a cancellable command for a task without a return value.
		/// </summary>
		public static AsyncCommand<object> Create(Func<CancellationToken, Task> command)
		{
			return new AsyncCommand<object>(async token => { await command(token); return null; });
		}

		/// <summary>
		/// Creates a cancellable command for a task that returns a result.
		/// </summary>
		public static AsyncCommand<TResult> Create<TResult>(Func<CancellationToken, Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(command);
		}
	}
}
