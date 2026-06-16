using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;

namespace VideohostingPanel.ViewModels.GenericViewModel
{
    public partial class EntityManagementViewModel<T> : BaseViewModel where T : class
    {
        private readonly ApiService _apiService;
        private readonly Tables _tableType;

        #region Свойства для списка

        [ObservableProperty]
        private ObservableCollection<T> _items = new();

        [ObservableProperty]
        private T? _selectedItem;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        private string? _sortBy;

        [ObservableProperty]
        private bool _sortAscending = true;

        [ObservableProperty]
        private string _sortDirection = "↑";

        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        [ObservableProperty]
        private bool _hasNextPage;

        [ObservableProperty]
        private bool _hasPreviousPage;

        #endregion

        #region Команды

        [RelayCommand]
        private async Task LoadData()
        {
            try
            {
                IsBusy = true;
                Items.Clear();

                var items = await _apiService.GetListAsync<T>(
                    CurrentPage,
                    PageSize,
                    SortBy,
                    SortAscending
                );

                foreach (var item in items)
                {
                    Items.Add(item);
                }

                TotalItems = await _apiService.GetCountAsync(_tableType.ToString().ToLower());
                HasNextPage = CurrentPage < TotalPages;
                HasPreviousPage = CurrentPage > 1;

                OnPropertyChanged(nameof(TotalPages));
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Ошибка загрузки данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                await LoadDataCommand.ExecuteAsync(null);
            }
        }

        [RelayCommand]
        private async Task PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadDataCommand.ExecuteAsync(null);
            }
        }

        [RelayCommand]
        private async Task DeleteItem(T? item)
        {
            if (item == null) return;

            var result = ModernMessageBox.Show(
                "Вы уверены? Это действие необратимо",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                var deleteResult = await _apiService.DeleteAsync<T>(GetItemId(item));

                if (deleteResult.Success)
                {
                    ModernMessageBox.Show(
                        $"Запись удалена\nИзмененные таблицы:\n" +
                        $"{DisplayChangesHelper.GetAffectedTablesString(deleteResult.AffectedTables)}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDataCommand.ExecuteAsync(null);
                }
                else
                {
                    ModernMessageBox.Show(
                        $"Ошибка удаления: {deleteResult.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenCreatePage()
        {
            OnNavigateRequested?.Invoke(NavigationAction.Create, null);
        }

        [RelayCommand]
        private void OpenEditPage(T? item)
        {
            if (item != null)
            {
                OnNavigateRequested?.Invoke(NavigationAction.Edit, item);
            }
        }

        [RelayCommand]
        private async Task Sort(string columnName)
        {
            if (SortBy == columnName)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SortBy = columnName;
                SortAscending = true;
            }

            SortDirection = SortAscending ? "↑" : "↓";
            await LoadDataCommand.ExecuteAsync(null);
        }

        #endregion

        #region Навигация

        public event Action<NavigationAction, object?>? OnNavigateRequested;

        #endregion

        #region Конструктор

        public EntityManagementViewModel(ApiService apiService, Tables tableType, string title)
        {
            _apiService = apiService;
            _tableType = tableType;
            Title = title;
            _ = LoadDataCommand.ExecuteAsync(null);
        }

        #endregion

        #region Вспомогательные методы

        private int GetItemId(T item)
        {
            // Используем рефлексию для получения Id
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null)
            {
                return (int)idProperty.GetValue(item)!;
            }
            throw new InvalidOperationException($"Тип {typeof(T).Name} не содержит свойство Id");
        }

        #endregion
    }
}

