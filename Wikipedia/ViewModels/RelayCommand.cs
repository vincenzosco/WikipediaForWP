using System;
using System.Windows.Input;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Synchronous ICommand used by every view model.
    /// This is the WinRT app model, where the raw WinRT name
    /// <c>Windows.UI.Xaml.Input.ICommand</c> cannot be referenced from C#
    /// (the projection keeps it out of reach), so
    /// <c>System.Windows.Input.ICommand</c> is the interface XAML binds
    /// against and the one to implement here.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute)
            : this(ToParameterizedAction(execute), null)
        {
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>Re-evaluates <see cref="CanExecute"/> on the next bind pass.</summary>
        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static Action<object> ToParameterizedAction(Action execute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            return parameter => execute();
        }
    }
}
