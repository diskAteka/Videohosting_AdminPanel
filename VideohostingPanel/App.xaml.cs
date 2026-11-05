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
            services.AddTransient<MainViewModel>();
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
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();

            ServiceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            // Прогрев libvlc в фоне: первый libvlc_new занимает секунды (скан плагинов).
            // Пока пользователь логинится, DLL и кэш плагинов уже загружены,
            // поэтому открытие страницы проверки видео не блокирует UI.
            VlcBootstrapper.WarmUpInBackground();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // libvlc держит фоновые потоки: без явного release процесс может
            // висеть после закрытия окна.
            VlcBootstrapper.Shutdown();

            base.OnExit(e);
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
                // Записать crash.log не удалось — сообщить об этом некуда.
            }
        }
    }
}