using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VideohostingPanel.Services
{
    public class AuthTokenHandler : DelegatingHandler
    {
        private string? _token;

        public string? Token => _token;
        public string? Role { get; private set; }
        public string? UserName { get; private set; }

        public void SetToken(string? token)
        {
            _token = token;
            ParseClaims();
        }

        public bool HasToken => !string.IsNullOrEmpty(_token);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                OnTokenExpired?.Invoke();
            return response;
        }

        private void ParseClaims()
        {
            Role = null;
            UserName = null;

            if (string.IsNullOrEmpty(_token))
                return;

            try
            {
                var parts = _token.Split('.');
                if (parts.Length < 2)
                    return;

                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Role = GetString(root, "role")
                    ?? GetString(root, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

                UserName = GetString(root, "unique_name")
                    ?? GetString(root, "name")
                    ?? GetString(root, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            }
            catch
            {
                // Некорректный токен — оставляем роль неопределённой
            }
        }

        private static string? GetString(JsonElement root, string property)
        {
            return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        public event Action? OnTokenExpired;
    }
}
