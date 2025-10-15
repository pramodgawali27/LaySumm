using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Platform.Api;
using Platform.Api.Validation;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Validator.Api.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Validator API",
        Version = "v1",
        Description = "Readability and factual validation for generated summaries."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration, options =>
{
    options.AddPolicy("ValidatorWriters", policy => policy.RequireRole("admin", "svc.gateway", "svc.orchestrator", "svc.batch"));
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<SummaryValidator>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient("fact-checker")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await ErrorResults.InternalServerError(context).ExecuteAsync(context);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapPost("/api/validator/evaluations", (ValidationRequest request, SummaryValidator validator) =>
    {
        var result = validator.Validate(request);
        return Results.Ok(result);
    })
    .AddEndpointFilter(new ValidationFilter<ValidationRequest>())
    .Produces<ValidationResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .RequireAuthorization("ValidatorWriters")
    .WithName("ValidateSummary");

app.MapPost("/api/validator/readability-preview", (ReadabilityPreviewRequest request, SummaryValidator validator) =>
    {
        var readability = validator.CalculateReadability(request.Text);
        return Results.Ok(readability);
    })
    .AddEndpointFilter(new ValidationFilter<ReadabilityPreviewRequest>())
    .Produces<ReadabilityPreviewResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .RequireAuthorization();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(4, TimeSpan.FromSeconds(30));

public sealed class SummaryValidator
{
    private static readonly Regex NumberRegex = new("\\b\\d+(?:\\.\\d+)?\\b", RegexOptions.Compiled);
    private static readonly string[] JargonBanList =
    {
        "etiology", "pathognomonic", "idiopathic", "iatrogenic", "morbid"
    };

    public ValidationResponse Validate(ValidationRequest request)
    {
        var readability = CalculateFleschReadingEase(request.PlainLanguageSummary);
        var meetsReadability = readability >= request.MinimumReadabilityScore;
        var containsJargon = ContainsJargon(request.PlainLanguageSummary);

        var numericFindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<string>();
        var numericConsistency = true;

        var numbers = NumberRegex.Matches(request.PlainLanguageSummary).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var number in numbers)
        {
            var evidence = request.Citations.FirstOrDefault(citation => citation.Snippet.Contains(number, StringComparison.OrdinalIgnoreCase));
            if (evidence is null)
            {
                numericConsistency = false;
                issues.Add($"Value '{number}' not found in citations.");
            }
            else
            {
                numericFindings[number] = evidence.SourceId;
            }
        }

        if (containsJargon)
        {
            issues.Add("Summary contains banned jargon.");
        }

        if (!meetsReadability)
        {
            issues.Add($"Readability score {readability:F1} is below threshold {request.MinimumReadabilityScore:F1}.");
        }

        return new ValidationResponse(
            request.SummaryId,
            readability,
            meetsReadability,
            containsJargon,
            numericConsistency,
            issues,
            numericFindings
        );
    }

    public ReadabilityPreviewResponse CalculateReadability(string text)
    {
        var fre = CalculateFleschReadingEase(text);
        var grade = CalculateFleschKincaidGrade(text);
        return new ReadabilityPreviewResponse(fre, grade);
    }

    private static bool ContainsJargon(string text) =>
        JargonBanList.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static double CalculateFleschReadingEase(string text)
    {
        var sentenceCount = Math.Max(1, text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length);
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var wordCount = Math.Max(1, words.Length);
        var syllableCount = words.Sum(CountSyllables);

        var wordsPerSentence = wordCount / (double)sentenceCount;
        var syllablesPerWord = syllableCount / (double)wordCount;

        return 206.835 - (1.015 * wordsPerSentence) - (84.6 * syllablesPerWord);
    }

    private static double CalculateFleschKincaidGrade(string text)
    {
        var sentenceCount = Math.Max(1, text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length);
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var wordCount = Math.Max(1, words.Length);
        var syllableCount = words.Sum(CountSyllables);

        return (0.39 * (wordCount / (double)sentenceCount)) + (11.8 * (syllableCount / (double)wordCount)) - 15.59;
    }

    private static int CountSyllables(string word)
    {
        var vowels = "aeiouy";
        var lower = word.ToLowerInvariant();
        var count = 0;
        var previousWasVowel = false;

        foreach (var ch in lower)
        {
            var isVowel = vowels.Contains(ch);
            if (isVowel && !previousWasVowel)
            {
                count++;
            }
            previousWasVowel = isVowel;
        }

        if (lower.EndsWith("e", StringComparison.OrdinalIgnoreCase) && count > 1)
        {
            count--;
        }

        return Math.Max(1, count);
    }
}

public partial class Program;
