using MVVMDialogs.ViewModels;

namespace MVVMDialogs.Presenters
{
    public interface IDialogBoxPresenter<T> where T : IDialogViewModel
    {
        void Show(T viewModel);
    }
}