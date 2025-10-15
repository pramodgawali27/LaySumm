using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.AgentFramework.Agents;

public static class AgentRegistrationExtensions
{
    public static IServiceCollection AddLaySummAgents(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var section = configuration?.GetSection("Agents");

        if (section is not null && section.Exists())
        {
            services.Configure<PlainLanguageAgentInstructions>(section);
        }
        else
        {
            services.Configure<PlainLanguageAgentInstructions>(_ => { });
        }

        services.AddSingleton<ILaySummAgentBlueprints, LaySummAgentBlueprints>();
        return services;
    }
}
