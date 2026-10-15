using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Minimal INotifyPropertyChanged base shared by every view model.
    /// <see cref="CallerMemberNameAttribute"/> keeps property names out of
    /// string literals, which is available on this C# version.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>True while a long running operation is in progress.</summary>
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
            protected set { SetProperty(ref _isBusy, value); }
        }

        private string _errorMessage;

        /// <summary>User facing error text, or null when the last action succeeded.</summary>
        public string ErrorMessage
        {
            get { return _errorMessage; }
            protected set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    RaisePropertyChanged("HasError");
                }
            }
        }

        /// <summary>True when <see cref="ErrorMessage"/> holds something to show.</summary>
        public bool HasError
        {
            get { return !string.IsNullOrEmpty(_errorMessage); }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
