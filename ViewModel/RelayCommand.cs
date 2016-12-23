/*
 * Created by SharpDevelop.
 * User: rotts
 * Date: 02.12.2016
 * Time: 22:32
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows.Input;

namespace RFiDGear.ViewModel
{
	/// <summary>
	/// Description of RelayCommand.
	/// </summary>
	public class RelayCommand : ICommand
	{
		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}
		private Action methodToExecute;
		private Func<bool> canExecuteEvaluator;
		public RelayCommand(Action methodToExecute, Func<bool> canExecuteEvaluator)
		{
			this.methodToExecute = methodToExecute;
			this.canExecuteEvaluator = canExecuteEvaluator;
		}
		public RelayCommand(Action methodToExecute)
			: this(methodToExecute, null)
		{
		}
		public bool CanExecute(object parameter)
		{
			if (this.canExecuteEvaluator == null)
			{
				return true;
			}
			else
			{
				bool result = this.canExecuteEvaluator.Invoke();
				return result;
			}
		}
		public void Execute(object parameter)
		{
			this.methodToExecute.Invoke();
		}
	}
}
