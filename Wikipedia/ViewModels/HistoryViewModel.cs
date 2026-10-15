using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>Reading history, most recent first.</summary>
    public sealed class HistoryViewModel : ViewModelBase
    {
        private readonly IHistoryStore _history;
        private readonly INavigationService _navigation;
        private readonly Localizer _localizer;

        public HistoryViewModel(IHistoryStore history, INavigationService navigation, Localizer localizer)
        {
            if (history == null) throw new ArgumentNullException("history");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _history = history;
            _navigation = navigation;
            _localizer = localizer;

            Entries = new ObservableCollection<HistoryEntry>();

            OpenCommand = new RelayCommand(Open);
            ClearCommand = new AsyncRelayCommand(parameter => ClearAsync());
        }

        public ObservableCollection<HistoryEntry> Entries { get; private set; }

        public bool IsEmpty
        {
            get { return Entries.Count == 0 && !IsBusy; }
        }

        public string EmptyStateText
        {
            get { return _localizer.Get("History_Empty"); }
        }

        public RelayCommand OpenCommand { get; private set; }
        public AsyncRelayCommand ClearCommand { get; private set; }

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var entries = await _history.GetAllAsync(CancellationToken.None);

                Entries.Clear();

                foreach (var entry in entries)
                {
                    Entries.Add(entry);
                }
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
            finally
            {
                IsBusy = false;
                RaisePropertyChanged("IsEmpty");
            }
        }

        private void Open(object parameter)
        {
            var entry = parameter as HistoryEntry;

            if (entry != null)
            {
                _navigation.GoToArticle(entry.Title);
            }
        }

        private async Task ClearAsync()
        {
            try
            {
                await _history.ClearAsync(CancellationToken.None);
                Entries.Clear();
                RaisePropertyChanged("IsEmpty");
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
        }
    }
}
