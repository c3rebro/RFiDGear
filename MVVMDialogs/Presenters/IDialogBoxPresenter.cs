using MvvmDialogs.ViewModels;
using System;
using System.Linq;


namespace MvvmDialogs.Presenters
{
	public interface IDialogBoxPresenter<T> where T : IDialogViewModel
	{
		void Show(T viewModel);
	}
}
