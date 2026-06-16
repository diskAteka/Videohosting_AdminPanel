using SharedLib.Contracts.Response.Auth;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace VideohostingPanel.Services
{
    public class AuthService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        // Путь к эндпоинту авторизации
        private const string AUTH_PATH = "/api/auth/login";

        public AuthService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<LoginResponse> LoginAsync(string baseUrl, string username, string password)
        {
            // Создаём отдельный HttpClient для авторизации (без токена)
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
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