using MvvmDialogs.ViewModels;

namespace MvvmDialogs.Presenters
{
    public interface IDialogBoxPresenter<T> where T : IDialogViewModel
    {
        void Show(T viewModel);
    }
}