using System;
using System.Linq;

namespace MvvmDialogs.ViewModels
{
	public interface IUserDialogViewModel : IDialogViewModel
	{
		bool IsModal { get; }
		void RequestClose();
		event EventHandler DialogClosing;
	}
}
