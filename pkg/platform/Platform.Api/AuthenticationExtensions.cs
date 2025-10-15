using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Platform.Api;

public static class AuthenticationExtensions
{
    public const string TestScheme = "Test";

    public static IServiceCollection AddPlatformSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthorizationOptions>? configure = null)
    {
        var authSection = configuration.GetSection("Auth");
        var disabled = authSection.GetValue("Disabled", false);

        if (disabled)
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, PermitAllAuthenticationHandler>(TestScheme, _ => { });

            services.AddAuthorization(options =>
            {
                configure?.Invoke(options);
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(TestScheme)
                    .RequireAssertion(_ => true)
                    .Build();
            });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authSection["Authority"];
                    options.Audience = authSection["Audience"];
                    var insecure = authSection.GetValue("AllowInsecureMetadata", false);
                    options.RequireHttpsMetadata = !insecure;
                });

            services.AddAuthorization(options => configure?.Invoke(options));
        }

        return services;
    }

    private sealed class PermitAllAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PermitAllAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "lay-summ-test"),
                new Claim(ClaimTypes.Name, "LaySumm Test Principal"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Role, "svc.gateway"),
                new Claim(ClaimTypes.Role, "svc.ingestion"),
                new Claim(ClaimTypes.Role, "svc.orchestrator"),
                new Claim(ClaimTypes.Role, "svc.batch"),
                new Claim(ClaimTypes.Role, "svc.scheduler")
            }, TestScheme);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
