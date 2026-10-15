using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wikipedia.Models;
using Wikipedia.Services;

namespace Wikipedia.ViewModels
{
    /// <summary>Saved articles, available offline.</summary>
    public sealed class SavedViewModel : ViewModelBase
    {
        private readonly ISavedArticleStore _saved;
        private readonly INavigationService _navigation;
        private readonly Localizer _localizer;

        public SavedViewModel(ISavedArticleStore saved, INavigationService navigation, Localizer localizer)
        {
            if (saved == null) throw new ArgumentNullException("saved");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (localizer == null) throw new ArgumentNullException("localizer");

            _saved = saved;
            _navigation = navigation;
            _localizer = localizer;

            Articles = new ObservableCollection<SavedArticle>();

            OpenCommand = new RelayCommand(Open);
            RemoveCommand = new AsyncRelayCommand(parameter => RemoveAsync(parameter as SavedArticle));
        }

        public ObservableCollection<SavedArticle> Articles { get; private set; }

        /// <summary>True when there is nothing saved, so the page shows its empty state.</summary>
        public bool IsEmpty
        {
            get { return Articles.Count == 0 && !IsBusy; }
        }

        public string EmptyStateText
        {
            get { return _localizer.Get("Saved_Empty"); }
        }

        public RelayCommand OpenCommand { get; private set; }
        public AsyncRelayCommand RemoveCommand { get; private set; }

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var articles = await _saved.GetAllAsync(CancellationToken.None);

                Articles.Clear();

                foreach (var article in articles)
                {
                    Articles.Add(article);
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
            var article = parameter as SavedArticle;

            if (article != null)
            {
                _navigation.GoToArticle(article.Title);
            }
        }

        private async Task RemoveAsync(SavedArticle article)
        {
            if (article == null)
            {
                return;
            }

            try
            {
                await _saved.RemoveAsync(article.Title, CancellationToken.None);
                Articles.Remove(article);
                RaisePropertyChanged("IsEmpty");
            }
            catch (Exception)
            {
                ErrorMessage = _localizer.Get("Error_Generic");
            }
        }
    }
}
