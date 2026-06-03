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
        private readonly Employee _originalEmployee;

        #region Свойства для разметки
        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private Employee _employee;

        [ObservableProperty]
        private ObservableCollection<ComboBoxItem> _roles = new()
        {
            new ComboBoxItem() { Id = 0, DisplayName = Services.Roles.Admin.ToString() },
            new ComboBoxItem() { Id = 1, DisplayName = Services.Roles.Verifier.ToString() },
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
                if(string.IsNullOrEmpty(Employee.UserName))
                {
                    ModernMessageBox.Show("Укажите имя сотрудника", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(Employee.Password))
                {
                    ModernMessageBox.Show("Укажите пароль сотрудника", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (IsEditMode) //Если объект реадктируется
                {
                    var request = new PostPutEmployeeRequest
                    {
                        UserName = Employee.UserName,
                        Password = Employee.Password,
                        Role = SelectedRole.DisplayName
                    };
                }
            }
            catch (Exception ex) {

        #endregion

    }
}
