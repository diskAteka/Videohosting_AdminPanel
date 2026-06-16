using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;
using VideohostingPanel.ViewModels.Factory;
using VideohostingPanel.ViewModels.GenericViewModel;
using VideohostingPanel.ViewModels.VerifyViewModels;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;

namespace VideohostingPanel.ViewModels.WindowViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly AuthApiService _authApiService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AuthTokenHandler _tokenHandler;
        private readonly ViewModelFactory _viewModelFactory;

        [ObservableProperty]
        private BaseViewModel? _currentView;

        #region Конструктор

        public MainViewModel(
            ApiService apiService,
            AuthApiService authApiService,
            IServiceProvider serviceProvider,
            AuthTokenHandler tokenHandler,
            ViewModelFactory viewModelFactory)
        {
            _apiService = apiService;
            _authApiService = authApiService;
            _serviceProvider = serviceProvider;
            _tokenHandler = tokenHandler;
            _viewModelFactory = viewModelFactory;

            Title = "Панель администратора";

            _tokenHandler.OnTokenExpired += () =>
                Application.Current.Dispatcher.BeginInvoke(() => Logout());
        }

        #endregion

        #region Навигация по сущностям

        [RelayCommand]
        private void ShowVideos()
        {
            var viewModel = new EntityManagementViewModel<GetVideoResponse>(
                _apiService, Tables.Video, "Управление видео");
            viewModel.OnNavigateRequested += OnVideoNavigate;
            CurrentView = viewModel;
        }

        [RelayCommand]
        private void ShowUsers()
        {
            var viewModel = new EntityManagementViewModel<GetUserResponse>(
                _apiService, Tables.User, "Управление пользователями");
            viewModel.OnNavigateRequested += OnUserNavigate;
            CurrentView = viewModel;
        }

        [RelayCommand]
        private void ShowComments()
        {
            var viewModel = new EntityManagementViewModel<GetCommentResponse>(
                _apiService, Tables.Comment, "Управление комментариями");
            viewModel.OnNavigateRequested += OnCommentNavigate;
            CurrentView = viewModel;
        }

        [RelayCommand]
        private void ShowEmployees()
        {
            var viewModel = new EntityManagementViewModel<Employee>(
                _apiService, Tables.Emploee, "Управление сотрудниками");
            viewModel.OnNavigateRequested += OnEmployeeNavigate;
            CurrentView = viewModel;
        }

        [RelayCommand]
        private void ShowAddVideo()
        {
            var viewModel = _viewModelFactory.CreateAddVideoViewModel();
            viewModel.OnGoBackRequested += () => ShowVideosCommand.Execute(null);
            CurrentView = viewModel;
        }

        [RelayCommand]
        private void ShowVerifyVideos()
        {
            var viewModel = new EntityManagementViewModel<GetVideoResponse>(
                _apiService, Tables.Video, "Верификация видео");
            viewModel.OnNavigateRequested += async (action, item) =>
            {
                if (action == NavigationAction.Edit && item is GetVideoResponse video)
                {
                    var verifyViewModel = new VerifyVideoViewModel(_apiService)
                    {
                        Video = video
                    };
                    verifyViewModel.OnGoBackRequested += () => ShowVerifyVideosCommand.Execute(null);
                    CurrentView = verifyViewModel;
                    await verifyViewModel.LoadVideoCommand.ExecuteAsync(null);
                }
            };
            CurrentView = viewModel;
        }

        #endregion

        #region Обработчики навигации

        private void OnVideoNavigate(NavigationAction action, object? item)
        {
            switch (action)
            {
                case NavigationAction.Create:
                    ShowAddVideoCommand.Execute(null);
                    break;
                case NavigationAction.Edit:
                    if (item is GetVideoResponse video)
                    {
                        var viewModel = _viewModelFactory.CreateEditVideoViewModel(video);
                        viewModel.OnGoBackRequested += () => ShowVideosCommand.Execute(null);
                        CurrentView = viewModel;
                    }
                    break;
                case NavigationAction.Back:
                    ShowVideosCommand.Execute(null);
                    break;
            }
        }

        private void OnUserNavigate(NavigationAction action, object? item)
        {
            switch (action)
            {
                case NavigationAction.Create:
                    var createViewModel = _viewModelFactory.CreateEditUserViewModel();
                    createViewModel.OnGoBackRequested += () => ShowUsersCommand.Execute(null);
                    CurrentView = createViewModel;
                    break;
                case NavigationAction.Edit:
                    if (item is GetUserResponse user)
                    {
                        var editViewModel = _viewModelFactory.CreateEditUserViewModel(user);
                        editViewModel.OnGoBackRequested += () => ShowUsersCommand.Execute(null);
                        CurrentView = editViewModel;
                    }
                    break;
            }
        }

        private void OnCommentNavigate(NavigationAction action, object? item)
        {
            switch (action)
            {
                case NavigationAction.Create:
                    var createViewModel = _viewModelFactory.CreateEditCommentViewModel();
                    createViewModel.OnGoBackRequested += () => ShowCommentsCommand.Execute(null);
                    CurrentView = createViewModel;
                    break;
                case NavigationAction.Edit:
                    if (item is GetCommentResponse comment)
                    {
                        var editViewModel = _viewModelFactory.CreateEditCommentViewModel(comment);
                        editViewModel.OnGoBackRequested += () => ShowCommentsCommand.Execute(null);
                        CurrentView = editViewModel;
                    }
                    break;
            }
        }

        private void OnEmployeeNavigate(NavigationAction action, object? item)
        {
            switch (action)
            {
                case NavigationAction.Create:
                    var createViewModel = _viewModelFactory.CreateEditEmloyeeViewModel();
                    createViewModel.OnGoBackRequested += () => ShowEmployeesCommand.Execute(null);
                    CurrentView = createViewModel;
                    break;
                case NavigationAction.Edit:
                    if (item is Employee employee)
                    {
                        var editViewModel = _viewModelFactory.CreateEditEmloyeeViewModel(employee);
                        editViewModel.OnGoBackRequested += () => ShowEmployeesCommand.Execute(null);
                        CurrentView = editViewModel;
                    }
                    break;
            }
        }

        #endregion

        [RelayCommand]
        private void Logout()
        {
            _authApiService.ClearAuth();
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.Close();
        }
    }
}