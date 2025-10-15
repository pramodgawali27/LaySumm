using System.Text.Json;

namespace GatewayBff.Api.External;

internal static class DefaultJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new(JsonSerializerDefaults.Web);
}
