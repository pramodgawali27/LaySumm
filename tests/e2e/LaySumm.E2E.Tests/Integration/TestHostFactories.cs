using GatewayBff.Api.External;
using Ingestion.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SummarizerA3.Api;
using Validator.Api;
using BatchOrchestratorA5.Api;

namespace LaySumm.E2E.Tests.Integration;

internal sealed class IngestionApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:Disabled", "true");
    }
}

internal sealed class SummarizerA3ApiFactory : WebApplicationFactory<SummarizerA3.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:Disabled", "true");
    }
}

internal sealed class ValidatorApiFactory : WebApplicationFactory<Validator.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:Disabled", "true");
    }
}

internal sealed class BatchApiFactory : WebApplicationFactory<BatchOrchestratorA5.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:Disabled", "true");
    }
}

internal sealed class GatewayApiFactory : WebApplicationFactory<GatewayBff.Api.Program>
{
    private readonly IngestionApiFactory _ingestionFactory;
    private readonly SummarizerA3ApiFactory _summarizerFactory;
    private readonly ValidatorApiFactory _validatorFactory;
    private readonly BatchApiFactory _batchFactory;

    public GatewayApiFactory(
        IngestionApiFactory ingestionFactory,
        SummarizerA3ApiFactory summarizerFactory,
        ValidatorApiFactory validatorFactory,
        BatchApiFactory batchFactory)
    {
        _ingestionFactory = ingestionFactory;
        _summarizerFactory = summarizerFactory;
        _validatorFactory = validatorFactory;
        _batchFactory = batchFactory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:Disabled", "true");
        builder.UseSetting("Services:IngestionBaseUrl", _ingestionFactory.Server.BaseAddress.ToString());
        builder.UseSetting("Services:SummarizerA3BaseUrl", _summarizerFactory.Server.BaseAddress.ToString());
        builder.UseSetting("Services:ValidatorBaseUrl", _validatorFactory.Server.BaseAddress.ToString());
        builder.UseSetting("Services:BatchBaseUrl", _batchFactory.Server.BaseAddress.ToString());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IIngestionClient));
            services.RemoveAll(typeof(ISummarizerA3Client));
            services.RemoveAll(typeof(IValidatorClient));
            services.RemoveAll(typeof(IBatchClient));

            services.AddSingleton<IIngestionClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IngestionClient>>();
                return new IngestionClient(_ingestionFactory.CreateClient(), logger);
            });

            services.AddSingleton<ISummarizerA3Client>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SummarizerA3Client>>();
                return new SummarizerA3Client(_summarizerFactory.CreateClient(), logger);
            });

            services.AddSingleton<IValidatorClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ValidatorClient>>();
                return new ValidatorClient(_validatorFactory.CreateClient(), logger);
            });

            services.AddSingleton<IBatchClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<BatchClient>>();
                return new BatchClient(_batchFactory.CreateClient(), logger);
            });
        });
    }
}
