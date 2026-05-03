using System.Net;

namespace WebApp.Application.Services.Yonetim
{
    public class ApiClientException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? Field { get; }

        public ApiClientException(HttpStatusCode statusCode, string message, string? field = null)
            : base(message)
        {
            StatusCode = statusCode;
            Field = field;
        }
    }
}
