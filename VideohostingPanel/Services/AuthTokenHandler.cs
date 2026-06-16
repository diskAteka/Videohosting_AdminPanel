using System.Net.Http;
using System.Net.Http.Headers;

namespace VideohostingPanel.Services
{
    public class AuthTokenHandler : DelegatingHandler
    {
        private string? _token;

        public void SetToken(string? token) => _token = token;
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



        public event Action? OnTokenExpired;
    }
}