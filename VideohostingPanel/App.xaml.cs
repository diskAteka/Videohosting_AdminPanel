using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using VideohostingPanel.Interfaces;
using VideohostingPanel.Services;
using VideohostingPanel.Views.Windows;
using WpfApiClient.DI;

namespace VideohostingPanel
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly string _url = "https://localhost:5001"; 
        public App()
        {
            var services = new ServiceCollection();

            services.AddWpfApiClient(_url);
            services.AddSingleton<IApiService, ApiService>();
            //services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            base.OnStartup(e);
        }

        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }
    }

}
