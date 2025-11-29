using MVVMDialogs.ViewModels.Interfaces;

namespace MVVMDialogs.Presenters.Interfaces
{
    public interface IDialogBoxPresenter<T> where T : IDialogViewModel
    {
        void Show(T viewModel);
    }
}
