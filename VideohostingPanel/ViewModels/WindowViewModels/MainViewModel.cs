using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.AddViewModels;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.ViewModels.EditViewModels;
using VideohostingPanel.ViewModels.VerifyViewModels;
using VideohostingPanel.Views.Elements;
using VideohostingPanel.Views.Pages;
using VideohostingPanel.Views.Pages.AddPages;
using VideohostingPanel.Views.Pages.EditPages;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;
using ComboBoxItem = VideohostingPanel.Services.ComboBoxItem;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly AuthApiService _authApiService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthTokenHandler _tokenHandler;

        #region Свойства для MainWindow

        [ObservableProperty]
        private bool _isAdmin = true;

        [ObservableProperty]
        private bool _isVerifier;

        [ObservableProperty]
        private string _userDisplayName = "Администратор";

        [ObservableProperty]
        private ObservableCollection<ComboBoxItem> _availableTables = new();

        [ObservableProperty]
        private ComboBoxItem? _selectedTable;

        [ObservableProperty]
        private ObservableCollection<object> _records = new();

        // Поиск
        [ObservableProperty]
        private string _searchIdText = string.Empty;

        [ObservableProperty]
        private string _searchNameText = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isNameSearchVisible;

        [ObservableProperty]
        private bool _isTextSearchVisible;

        [ObservableProperty]
        private bool _isLogTypeVisible;

        [ObservableProperty]
        private bool _isAddButtonVisible = true;

        // Типы логов
        [ObservableProperty]
        private ObservableCollection<string> _logTypes = new()
        {
            "Все", "Info", "Warning", "Error", "Create", "Update", "Delete"
        };

        [ObservableProperty]
        private string _selectedLogType = "Все";

        // Пагинация
        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 10;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private string _pageInfo = string.Empty;

        [ObservableProperty]
        private string _totalItemsInfo = string.Empty;

        [ObservableProperty]
        private bool _sortIsAscending = true;

        // Для верификатора
        [ObservableProperty]
        private ObservableCollection<object> _unverifiedVideos = new();

        #endregion

        #region События для модальных страниц

        public event Action<BaseViewModel, Page>? ModalPageRequested;
        public event Action? HideModalPageRequested;

        #endregion

        #region Конструктор

        public MainViewModel(
            ApiService apiService,
            AuthApiService authApiService,
            IServiceProvider serviceProvider,
            AuthTokenHandler tokenHandler)
        {
            _apiService = apiService;
            _authApiService = authApiService;
            _serviceProvider = serviceProvider;
            _tokenHandler = tokenHandler;
            Title = "Видеохостинг - Панель управления";

            // Подписка на истечение токена
            _tokenHandler.OnTokenExpired += () =>
                Application.Current.Dispatcher.BeginInvoke(() => Logout());

            // Инициализация таблиц
            InitializeTables();
        }
        #endregion

        #region Инициализация

        private void InitializeTables()
        {
            AvailableTables = new ObservableCollection<ComboBoxItem>
            {
                new() { Id = (int)Tables.Video, DisplayName = "Видео" },
                new() { Id = (int)Tables.User, DisplayName = "Пользователи" },
                new() { Id = (int)Tables.Comment, DisplayName = "Комментарии" },
                new() { Id = (int)Tables.Like, DisplayName = "Лайки" },
                new() { Id = (int)Tables.DisLike, DisplayName = "Дизлайки" },
                new() { Id = (int)Tables.View, DisplayName = "Просмотры" },
                new() { Id = (int)Tables.ServerLog, DisplayName = "Логи" },
                new() { Id = (int)Tables.Emploee, DisplayName = "Сотрудники" }
            };

            SelectedTable = AvailableTables[0];
        }

        partial void OnSelectedTableChanged(ComboBoxItem? value)
        {
            if (value == null) return;
            UpdateSearchVisibility();
            _ = LoadRecordsCommand.ExecuteAsync(null);
        }

        private void UpdateSearchVisibility()
        {
            var table = (Tables)SelectedTable!.Id;
            IsNameSearchVisible = table is Tables.Video or Tables.User or Tables.Emploee;
            IsTextSearchVisible = table == Tables.Comment;
            IsLogTypeVisible = table == Tables.ServerLog;
            IsAddButtonVisible = table != Tables.ServerLog;
        }
        #endregion

        #region Команды загрузки данных

        [RelayCommand]
        private async Task LoadRecords()
        {
            try
            {
                IsBusy = true;
                Records.Clear();

                var table = (Tables)SelectedTable!.Id;

                switch (table)
                {
                    case Tables.Video:
                        await LoadVideosAsync();
                        break;
                    case Tables.User:
                        await LoadUsersAsync();
                        break;
                    case Tables.Comment:
                        await LoadCommentsAsync();
                        break;
                    case Tables.Like:
                        await LoadLikesAsync();
                        break;
                    case Tables.DisLike:
                        await LoadDislikesAsync();
                        break;
                    case Tables.View:
                        await LoadViewsAsync();
                        break;
                    case Tables.ServerLog:
                        await LoadLogsAsync();
                        break;
                    case Tables.Emploee:
                        await LoadEmployeesAsync();
                        break;
                }

                TotalItems = await _apiService.GetCountAsync(
                    SelectedTable.DisplayName.ToLower(), IsAdmin);

                UpdatePaginationInfo();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Ошибка загрузки: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadVideosAsync()
        {
            var videos = await _apiService.GetListAsync<GetVideoResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var video in videos)
                Records.Add(new VideoItemControl { DataContext = video });
        }

        private async Task LoadUsersAsync()
        {
            var users = await _apiService.GetListAsync<GetUserResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var user in users)
                Records.Add(new UserItemControl { DataContext = user });
        }

        private async Task LoadCommentsAsync()
        {
            var comments = await _apiService.GetListAsync<GetCommentResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var comment in comments)
                Records.Add(new CommentItemControl { DataContext = comment });
        }

        private async Task LoadLikesAsync()
        {
            var likes = await _apiService.GetListAsync<GetLikeResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var like in likes)
                Records.Add(new LikeItemControl { DataContext = like });
        }

        private async Task LoadDislikesAsync()
        {
            var dislikes = await _apiService.GetListAsync<GetDislikeResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var dislike in dislikes)
                Records.Add(new DislikeItemControl { DataContext = dislike });
        }

        private async Task LoadViewsAsync()
        {
            var views = await _apiService.GetListAsync<GetViewResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var view in views)
                Records.Add(new ViewItemControl { DataContext = view });
        }

        private async Task LoadLogsAsync()
        {
            var logs = await _apiService.GetListAsync<GetServerLogResponse>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var log in logs)
                Records.Add(new LogItemControl { DataContext = log });
        }

        private async Task LoadEmployeesAsync()
        {
            var employees = await _apiService.GetListAsync<Employee>(
                CurrentPage, PageSize, sortBy: null, SortIsAscending);
            foreach (var employee in employees)
                Records.Add(new EmployeeItemControl { DataContext = employee });
        }
        #endregion

        #region Пагинация и сортировка

        [RelayCommand]
        private async Task LoadNext()
        {
            CurrentPage++;
            await LoadRecordsCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task LoadPrevious()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadRecordsCommand.ExecuteAsync(null);
            }
        }

        [RelayCommand]
        private async Task SortAscending()
        {
            SortIsAscending = true;
            CurrentPage = 1;
            await LoadRecordsCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task SortDescending()
        {
            SortIsAscending = false;
            CurrentPage = 1;
            await LoadRecordsCommand.ExecuteAsync(null);
        }

        private void UpdatePaginationInfo()
        {
            var start = (CurrentPage - 1) * PageSize + 1;
            var end = Math.Min(CurrentPage * PageSize, TotalItems);
            PageInfo = TotalItems > 0 ? $"{start}-{end}" : "0-0";
            TotalItemsInfo = $"из {TotalItems}";
        }
        #endregion

        #region Поиск

        [RelayCommand]
        private async Task SearchById()
        {
            if (!int.TryParse(SearchIdText, out int id)) return;
            try
            {
                IsBusy = true;
                Records.Clear();
                var table = (Tables)SelectedTable!.Id;

                switch (table)
                {
                    case Tables.Video:
                        var video = await _apiService.GetByIdAsync<GetVideoResponse>(id);
                        Records.Add(new VideoItemControl { DataContext = video });
                        break;
                    case Tables.User:
                        var user = await _apiService.GetByIdAsync<GetUserResponse>(id);
                        Records.Add(new UserItemControl { DataContext = user });
                        break;
                    case Tables.Comment:
                        var comment = await _apiService.GetByIdAsync<GetCommentResponse>(id);
                        Records.Add(new CommentItemControl { DataContext = comment });
                        break;
                    case Tables.Like:
                        var like = await _apiService.GetByIdAsync<GetLikeResponse>(id);
                        Records.Add(new LikeItemControl { DataContext = like });
                        break;
                    case Tables.DisLike:
                        var dislike = await _apiService.GetByIdAsync<GetDislikeResponse>(id);
                        Records.Add(new DislikeItemControl { DataContext = dislike });
                        break;
                    case Tables.View:
                        var view = await _apiService.GetByIdAsync<GetViewResponse>(id);
                        Records.Add(new ViewItemControl { DataContext = view });
                        break;
                    case Tables.ServerLog:
                        var log = await _apiService.GetByIdAsync<GetServerLogResponse>(id);
                        Records.Add(new LogItemControl { DataContext = log });
                        break;
                    case Tables.Emploee:
                        var employee = await _apiService.GetByIdAsync<Employee>(id);
                        Records.Add(new EmployeeItemControl { DataContext = employee });
                        break;
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Не найдено: {ex.Message}",
                    "Поиск", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SearchByName()
        {
            await LoadRecordsCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task SearchByText()
        {
            await LoadRecordsCommand.ExecuteAsync(null);
        }
        #endregion

        #region Публичные методы навигации для ItemControl

        public void AddNewRecord()
        {
            var table = (Tables)SelectedTable!.Id;
            switch (table)
            {
                case Tables.Video:
                    OpenAddVideoWindow();
                    break;
                case Tables.User:
                    OpenEditUserWindow(null);
                    break;
                case Tables.Comment:
                    OpenEditCommentWindow(null);
                    break;
                case Tables.Emploee:
                    OpenEditEmployeeWindow(null);
                    break;
                default:
                    ModernMessageBox.Show("Для этой таблицы создание записей недоступно",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        public void OpenEditVideoWindow(GetVideoResponse? video)
        {
            var vm = new EditVideoViewModel(_apiService, video);
            vm.OnGoBackRequested += HideModalAndReload;
            ShowModalPage(vm, new EditVideo());
        }

        public void OpenEditUserWindow(GetUserResponse? user)
        {
            var vm = new EditUserViewModel(_apiService, user);
            vm.OnGoBackRequested += HideModalAndReload;
            ShowModalPage(vm, new EditUser());
        }

        public void OpenEditCommentWindow(GetCommentResponse? comment)
        {
            var vm = new EditCommentViewModel(_apiService, comment);
            vm.OnGoBackRequested += HideModalAndReload;
            ShowModalPage(vm, new EditComment());
        }

        public void OpenEditEmployeeWindow(Employee? employee)
        {
            var vm = new EditEmloyeeViewModel(_apiService, employee);
            vm.OnGoBackRequested += HideModalAndReload;
            ShowModalPage(vm, new EditEmployee());
        }

        public void OpenAddVideoWindow()
        {
            var vm = new AddVideoViewModel(_apiService);
            vm.OnGoBackRequested += HideModalAndReload;
            ShowModalPage(vm, new AddVideo());
        }

        public void OpenVerificationPage(GetVideoResponse video)
        {
            var vm = new VerifyVideoViewModel(_apiService) { Video = video };
            vm.OnGoBackRequested += HideModalAndReload;
            var page = new VerifyVideo(vm);
            ShowModalPage(vm, page);
            _ = vm.LoadVideoCommand.ExecuteAsync(null);
        }

#endregion

        #region Приватные методы для управления модальным режимом

        private void ShowModalPage(BaseViewModel viewModel, Page page)
        {
            page.DataContext = viewModel;
            ModalPageRequested?.Invoke(viewModel, page);
        }

        public void HideModalPage()
        {
            HideModalPageRequested?.Invoke();
        }

        private async void HideModalAndReload()
        {
            HideModalPage();
            // Небольшая задержка, чтобы UI успел скрыть фрейм до начала загрузки
            await System.Threading.Tasks.Task.Delay(100);
            await LoadRecordsCommand.ExecuteAsync(null);
        }

        #endregion

        #region Выход

        [RelayCommand]
        private void Logout()
        {
            _authApiService.ClearAuth();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();
        }

        [RelayCommand]
        private void CloseApplication()
        {
            Application.Current.Shutdown();
        }
        #endregion
    }
}