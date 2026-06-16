using Microsoft.Extensions.DependencyInjection;
using SharedLib.Contracts.Admin;
using SharedLib.Models;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.AddViewModels;
using VideohostingPanel.ViewModels.EditViewModels;

namespace VideohostingPanel.ViewModels.Factory
{
    public class ViewModelFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewModelFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public EditVideoViewModel CreateEditVideoViewModel(GetVideoResponse? video = null)
        {
            var apiService = _serviceProvider.GetRequiredService<ApiService>();
            return new EditVideoViewModel(apiService, video);
        }

        public EditUserViewModel CreateEditUserViewModel(GetUserResponse? user = null)
        {
            var apiService = _serviceProvider.GetRequiredService<ApiService>();
            return new EditUserViewModel(apiService, user);
        }

        public EditCommentViewModel CreateEditCommentViewModel(GetCommentResponse? comment = null)
        {
            var apiService = _serviceProvider.GetRequiredService<ApiService>();
            return new EditCommentViewModel(apiService, comment);
        }

        public EditEmloyeeViewModel CreateEditEmloyeeViewModel(Employee? employee = null)
        {
            var apiService = _serviceProvider.GetRequiredService<ApiService>();
            return new EditEmloyeeViewModel(apiService, employee);
        }

        public AddVideoViewModel CreateAddVideoViewModel()
        {
            var apiService = _serviceProvider.GetRequiredService<ApiService>();
            return new AddVideoViewModel(apiService);
        }
    }
}