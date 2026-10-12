using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

        private static readonly string CrashLogPath =
            Path.Combine(AppContext.BaseDirectory, "crash.log");

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndReport(e.Exception);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogAndReport(e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogAndReport(e.Exception);
            e.SetObserved();
        }

        private static void LogAndReport(Exception? exception)
        {
            if (exception == null)
                return;

            try
            {
                File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
            }

            DebugLog.Write($"Unhandled: {exception}");
        }
    }
}