using SharedLib.Contracts.Response.Auth;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WpfApiClient.Config;

namespace VideohostingPanel.Services
{
    public class AuthService
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private const string AUTH_PATH = AppConfig.AUTH_PATH;
        private const int TIMEOUT_SECONDS = AppConfig.AUTH_TIMEOUT_SECONDS;

        public AuthService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<LoginResponse> LoginAsync(string baseUrl, string username, string password)
        {    
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS)
            };

            var request = new
            {
                username = username,
                password = password
            };

            var response = await httpClient.PostAsJsonAsync(AUTH_PATH, request, _jsonOptions);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content
                .ReadFromJsonAsync<LoginResponse>(_jsonOptions);

            return loginResponse
                ?? throw new InvalidOperationException("Не удалось получить ответ от сервера");
        }
    }
}