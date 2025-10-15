namespace GatewayBff.Api;

public sealed class GatewayServiceOptions
{
    public string IngestionBaseUrl { get; set; } = "http://localhost:5201";
    public string SummarizerA3BaseUrl { get; set; } = "http://localhost:5202";
    public string ValidatorBaseUrl { get; set; } = "http://localhost:5203";
    public string BatchBaseUrl { get; set; } = "http://localhost:5204";
}
