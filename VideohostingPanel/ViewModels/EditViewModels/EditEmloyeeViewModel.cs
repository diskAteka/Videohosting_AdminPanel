using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using System.Collections.ObjectModel;
using System.Windows;
using VideohostingPanel.Resources.Elements;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.Base;

namespace VideohostingPanel.ViewModels.EditViewModels
{
    public partial class EditEmloyeeViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;

        #region Свойства для разметки

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private Employee _employee;

        [ObservableProperty]
        private ObservableCollection<ComboBoxItem> _roles = new()
        {
            new ComboBoxItem() { Id = 0, DisplayName = Services.Roles.Verifier.ToString() },
            new ComboBoxItem() { Id = 1, DisplayName = Services.Roles.Admin.ToString() },
        };

        [ObservableProperty]
        private ComboBoxItem? _selectedRole;

        #endregion

        #region Команды

        [RelayCommand]
        private async Task SaveChanges()
        {
            try
            {
                IsBusy = true;
                if (string.IsNullOrEmpty(Employee.UserName))
                {
                    ModernMessageBox.Show("Укажите имя сотрудника", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(Password))
                {
                    ModernMessageBox.Show("Укажите пароль сотрудника", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (SelectedRole == null)
                {
                    ModernMessageBox.Show("Укажите роль сотрудника", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var request = new PostPutEmployeeRequest
                {
                    UserName = Employee.UserName,
                    Password = Password,
                    Role = SelectedRole.DisplayName
                };
                if (IsEditMode) //Если объект реадктируется
                {
                    var response = await _apiService.UpdateAsync(Employee.Id, request);
                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Данные сотрудника обновлены\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Непредвиденная ошибка\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else //Если объект создается
                {
                    var response = await _apiService.AddAsync(request);

                    if (response.Success)
                    {
                        ModernMessageBox.Show($"Создан новый сотрудник\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        GoBack();
                    }
                    else
                    {
                        ModernMessageBox.Show($"Ошибка сервера\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    Password = string.Empty;
                }
            }
            catch (Exception ex) 
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (!IsEditMode) return;

            var answer = ModernMessageBox.Show(
                "Вы уверены? Это действие необратимо",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.No) return;

            try
            {
                IsBusy = true;
                var response = await _apiService.DeleteAsync<Employee>(Employee.Id);

                if(response.Success)
                {
                    ModernMessageBox.Show($"Сотрудник удален\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    GoBack();
                }
                else
                {
                    ModernMessageBox.Show($"Ошибка сервера\nИзмененные таблицы:\n" +
                            $" {DisplayChangesHelper.GetAffectedTablesString(response.AffectedTables)}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch(Exception ex)
            {
                ModernMessageBox.Show($"Внутренняя ошибка программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        private void GoBack()
        {
            OnGoBackRequested?.Invoke();
        }
        #endregion

        #region Навигация

        public event Action? OnGoBackRequested;

        #endregion

        #region Конструктор

        public EditEmloyeeViewModel(ApiService apiService, Employee? employee = null)
        {
            _apiService = apiService;
            _isEditMode = employee != null;

            if(_isEditMode && employee != null)
            {
                _employee = new Employee
                {
                    Id = employee.Id,
                    UserName = employee.UserName,
                    Password = employee.Password,
                    Role = employee.Role,
                };
                Password = employee.Password;
                Title = $"Редактирование сотрудника";
                SelectedRole = Roles.FirstOrDefault(r => r.DisplayName == employee.Role) ?? Roles[0];
            }
            else
            {
                _employee = new Employee
                {
                    UserName = string.Empty,
                    Password = string.Empty,
                    Role = Services.Roles.Verifier.ToString()
                };
                Title = $"Создание сотрудника";
                SelectedRole = Roles[0];
            }
        }

        #endregion


    }
}
