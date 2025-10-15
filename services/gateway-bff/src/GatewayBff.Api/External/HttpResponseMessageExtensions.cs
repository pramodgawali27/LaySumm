using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace GatewayBff.Api.External;

internal static class HttpResponseMessageExtensions
{
    public static async Task EnsureSuccessAsync(this HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null ? null : await response.Content.ReadAsStringAsync();
        throw new GatewayHttpException(operation, response.StatusCode, body);
    }
}

public sealed class GatewayHttpException : Exception
{
    public GatewayHttpException(string operation, HttpStatusCode statusCode, string? body)
        : base($"Request failed while performing '{operation}' with status {(int)statusCode} ({statusCode}).")
    {
        Operation = operation;
        StatusCode = statusCode;
        Body = body;
    }

    public string Operation { get; }

    public HttpStatusCode StatusCode { get; }

    public string? Body { get; }
}
