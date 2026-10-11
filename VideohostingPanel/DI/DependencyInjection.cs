using Microsoft.Extensions.DependencyInjection;
using WpfApiClient.Config;
using VideohostingPanel.Services;
using WpfApiClient.Interfaces;

namespace VideohostingPanel.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddWpfApiClient(this IServiceCollection services)
        {
            services.AddSingleton<AuthTokenHandler>();
            services.AddSingleton<AuthService>();

            services.AddHttpClient<IWpfApi, WpfApiClient.Services.WpfApiClient>(client =>
            {
                client.BaseAddress = new Uri(AppConfig.BuildBaseUrl(AppConfig.DEFAULT_SERVER, AppConfig.DEFAULT_PORT));
                client.Timeout = TimeSpan.FromSeconds(AppConfig.API_TIMEOUT_SECONDS);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddHttpMessageHandler<AuthTokenHandler>();

            return services;
        }
    }
}