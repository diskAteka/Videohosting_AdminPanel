using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private BaseViewModel? _currentView;

        public BaseViewModel? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public MainViewModel(ApiService apiService)
        {
            _apiService = apiService;
            Title = "Панель администратора";
        }


    }
}
