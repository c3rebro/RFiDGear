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
//using GalaSoft.MvvmLight;

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

		public ICommand CancelCommand
		{
			get { return _cancelCommand; }
		}

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
			if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
		}

		private sealed class CancelAsyncCommand : ICommand
		{
			private CancellationTokenSource _cts = new CancellationTokenSource();
			private bool _commandExecuting;

			public CancellationToken Token { get { return _cts.Token; } }

			public void NotifyCommandStarting()
			{
				_commandExecuting = true;
				if (!_cts.IsCancellationRequested)
					return;
				_cts = new CancellationTokenSource();
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

			public event EventHandler CanExecuteChanged
			{
				add { CommandManager.RequerySuggested += value; }
				remove { CommandManager.RequerySuggested -= value; }
			}

			private void RaiseCanExecuteChanged()
			{
				CommandManager.InvalidateRequerySuggested();
			}
		}
	}

	public static class AsyncCommand
	{
		public static AsyncCommand<object> Create(Func<Task> command)
		{
			return new AsyncCommand<object>(async _ => { await command(); return null; });
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(_ => command());
		}

		public static AsyncCommand<object> Create(Func<CancellationToken, Task> command)
		{
			return new AsyncCommand<object>(async token => { await command(token); return null; });
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<CancellationToken, Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(command);
		}
	}
}
//	public interface AsyncCommand<TParam>: ICommand, IDisposable
//	{
//
//	}
//
//	public class AsyncCommand : ICommand, IDisposable
//	{
//		private readonly BackgroundWorker _backgroundWorker = new BackgroundWorker {WorkerSupportsCancellation = true};
//		private readonly Func<bool> _canExecute;
//
//		public AsyncCommand(Action action, Func<bool> canExecute = null, Action<object> completed = null,
//		                    Action<Exception> error = null)
//		{
//			_backgroundWorker.DoWork += (s, e) =>
//			{
//				CommandManager.InvalidateRequerySuggested();
//				action();
//			};
//
//			_backgroundWorker.RunWorkerCompleted += (s, e) =>
//			{
//				if (completed != null && e.Error == null)
//					completed(e.Result);
//
//				if (error != null && e.Error != null)
//					error(e.Error);
//
//				CommandManager.InvalidateRequerySuggested();
//			};
//
//			_canExecute = canExecute;
//		}
//
//		public void Cancel()
//		{
//			if (_backgroundWorker.IsBusy)
//				_backgroundWorker.CancelAsync();
//		}
//
//		public bool CanExecute(object parameter)
//		{
//			return _canExecute == null
//				? !_backgroundWorker.IsBusy
//				: !_backgroundWorker.IsBusy && _canExecute();
//		}
//
//		public void Execute(object parameter)
//		{
//			_backgroundWorker.RunWorkerAsync();
//		}
//
//		public event EventHandler CanExecuteChanged
//		{
//			add { CommandManager.RequerySuggested += value; }
//			remove { CommandManager.RequerySuggested -= value; }
//		}
//
//		public void Dispose()
//		{
//			Dispose(true);
//			GC.SuppressFinalize(this);
//		}
//
//		protected virtual void Dispose(bool disposing)
//		{
//			if (disposing)
//			{
//				if (_backgroundWorker != null)
//					_backgroundWorker.Dispose();
//			}
//		}
//	}
//}
