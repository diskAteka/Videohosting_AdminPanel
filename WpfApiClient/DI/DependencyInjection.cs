using Microsoft.Extensions.DependencyInjection;
using WpfApiClient.Interfaces;
using WpfApiClient.Services;

namespace WpfApiClient.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddWpfApiClient(this IServiceCollection services, string baseUrl)
        {
            services.AddHttpClient<IWpfApi, Services.WpfApiClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            return services;
        }
    }
}