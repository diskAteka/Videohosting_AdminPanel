using Microsoft.Extensions.DependencyInjection;
using VideohostingPanel.Services;
using WpfApiClient.Interfaces;

namespace VideohostingPanel.DI
{
    public static class DependencyInjection
    {
        private const string DEFAULT_BASE_URL = "https://localhost:5001"; //Это значение может быть изменено в AuthApiService.SetBaseUrl

        public static IServiceCollection AddWpfApiClient(this IServiceCollection services)
        {
            services.AddSingleton<AuthTokenHandler>();
            services.AddSingleton<AuthService>();

            services.AddHttpClient<IWpfApi, WpfApiClient.Services.WpfApiClient>(client =>
            {
                client.BaseAddress = new Uri(DEFAULT_BASE_URL);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AuthTokenHandler>(); 
            services.AddSingleton<AuthApiService>();

            return services;
        }
    }
}