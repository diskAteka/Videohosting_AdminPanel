using System.Net;

namespace VideohostingPanel.Services
{
    public class LoginException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public LoginException(HttpStatusCode statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
