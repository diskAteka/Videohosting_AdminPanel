using SharedLib.Contracts.Admin;
using SharedLib.Contracts.Response.Auth;
using System.Net;
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

        public async Task<TokenResponse> LoginAsync(string baseUrl, string username, string password)
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS)
            };

            var request = new EmployeeLoginRequest
            {
                Username = username,
                Password = password
            };

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsJsonAsync(AUTH_PATH, request, _jsonOptions);
            }
            catch (HttpRequestException)
            {
                throw new LoginException(HttpStatusCode.ServiceUnavailable,
                    "Не удалось подключиться к серверу. Проверьте адрес и порт.");
            }
            catch (TaskCanceledException)
            {
                throw new LoginException(HttpStatusCode.RequestTimeout,
                    "Сервер не ответил. Превышено время ожидания.");
            }

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new LoginException(response.StatusCode, ExtractMessage(body));
            }

            var loginResponse = JsonSerializer.Deserialize<TokenResponse>(body, _jsonOptions);

            if (loginResponse == null)
            {
                throw new LoginException(response.StatusCode, "Не удалось получить ответ от сервера");
            }

            return loginResponse;
        }

        private static string ExtractMessage(string body)
        {
            try
            {
                var error = JsonSerializer.Deserialize<ApiErrorResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (!string.IsNullOrWhiteSpace(error?.Message))
                    return error!.Message!;
            }
            catch (JsonException)
            {
            }

            return string.IsNullOrWhiteSpace(body)
                ? "Не удалось выполнить вход. Попробуйте ещё раз."
                : body;
        }

        private sealed class ApiErrorResponse
        {
            public string? Message { get; set; }
        }
    }
}
