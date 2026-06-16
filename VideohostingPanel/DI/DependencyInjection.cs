using Microsoft.Extensions.DependencyInjection;
using VideohostingPanel.Services;
using WpfApiClient.Interfaces;
using WpfApiClient.Services;

namespace VideohostingPanel.DI
{
    public static class DependencyInjection
    {
        // Путь по умолчанию (будет переопределён при входе)
        private const string DEFAULT_BASE_URL = "https://localhost:5001";

        public static IServiceCollection AddWpfApiClient(this IServiceCollection services)
        {
            // Регистрируем обработчик токенов (Singleton — один на всё приложение)
            services.AddSingleton<AuthTokenHandler>();

            // Регистрируем AuthService (без HttpClient, создаёт свой временный)
            services.AddSingleton<AuthService>();

            // Регистрируем HttpClient с обработчиком токенов
            services.AddHttpClient<IWpfApi, WpfApiClient.Services.WpfApiClient>(client =>
            {
                client.BaseAddress = new Uri(DEFAULT_BASE_URL);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AuthTokenHandler>(); // Автоматическая вставка токена

            // Регистрируем AuthApiService (управление URL и токеном)
            services.AddSingleton<AuthApiService>();

            return services;
        }
    }
}