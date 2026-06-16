using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VideohostingPanel.DI;
using VideohostingPanel.Services;
using VideohostingPanel.ViewModels.AddViewModels;
using VideohostingPanel.ViewModels.EditViewModels;
using VideohostingPanel.ViewModels.VerifyViewModels;
using VideohostingPanel.ViewModels.WindowViewModels;
using VideohostingPanel.Views.Windows;
using Application = System.Windows.Application;

namespace VideohostingPanel
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            var services = new ServiceCollection();

            // API клиент с поддержкой токенов
            services.AddWpfApiClient();

            // Сервисы приложения
            services.AddSingleton<ApiService>();

            // Window ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();

            // Edit ViewModels
            services.AddTransient<EditVideoViewModel>();
            services.AddTransient<EditUserViewModel>();
            services.AddTransient<EditCommentViewModel>();
            services.AddTransient<EditEmloyeeViewModel>();

            // Add ViewModels
            services.AddTransient<AddVideoViewModel>();

            // Verify ViewModels
            services.AddTransient<VerifyVideoViewModel>();

            // Окна
            services.AddSingleton<MainWindow>();
            services.AddTransient<LoginWindow>();

            ServiceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            base.OnStartup(e);
        }
    }
}