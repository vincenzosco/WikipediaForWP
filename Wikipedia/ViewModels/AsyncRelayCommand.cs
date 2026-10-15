using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wikipedia.ViewModels
{
    /// <summary>
    /// Asynchronous ICommand with a re-entrancy guard, so a double tap cannot
    /// start the same network operation twice. Any exception the delegate lets
    /// through is routed to the optional error handler instead of escaping an
    /// async void method, which would terminate the process.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private readonly Action<Exception> _onError;
        private bool _isRunning;

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null, Action<Exception> onError = null)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            _execute = execute;
            _canExecute = canExecute;
            _onError = onError;
        }

        public bool CanExecute(object parameter)
        {
            if (_isRunning)
            {
                return false;
            }

            return _canExecute == null || _canExecute(parameter);
        }

        public async void Execute(object parameter)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            catch (Exception exception)
            {
                if (_onError != null)
                {
                    _onError(exception);
                }
            }
            finally
            {
                _isRunning = false;

                // Execute is always entered from the UI thread, so the
                // continuation of the await above resumes there as well and
                // this can be raised directly without marshalling.
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
