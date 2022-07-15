using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace MvvmDialogs.ViewModels
{
    public class MessageBoxViewModel : IDialogViewModel
    {
        private string _Caption = "";

        public string Caption
        {
            get => _Caption;
            set => _Caption = value;
        }

        private string _Message = "";

        public string Message
        {
            get => _Message;
            set => _Message = value;
        }

        private MessageBoxButton _Buttons = MessageBoxButton.OK;

        public MessageBoxButton Buttons
        {
            get => _Buttons;
            set => _Buttons = value;
        }

        private MessageBoxImage _Image = MessageBoxImage.None;

        public MessageBoxImage Image
        {
            get => _Image;
            set => _Image = value;
        }

        private MessageBoxResult _Result;

        public MessageBoxResult Result
        {
            get => _Result;
            set => _Result = value;
        }

        public MessageBoxViewModel(string message = "", string caption = "")
        {
            Message = message;
            Caption = caption;
        }

        public MessageBoxResult Show(IList<IDialogViewModel> collection)
        {
            collection.Add(this);
            return Result;
        }

        public bool HasResourceDictionary { get; private set; }
        public ResourceDictionary Resources { get; private set; }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged Members
    }
}