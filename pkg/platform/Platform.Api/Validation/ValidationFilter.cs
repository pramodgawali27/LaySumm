using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Platform.Api.Validation;

public sealed record FieldError(string Field, string Message);

public sealed record ErrorResponse(string Code, string Message, IReadOnlyCollection<FieldError>? Errors = null, string? TraceId = null);

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.FirstOrDefault(arg => arg is T) as T;
        if (argument is null)
        {
            return await next(context);
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(argument);
        if (!Validator.TryValidateObject(argument, validationContext, validationResults, validateAllProperties: true))
        {
            var fieldErrors = validationResults
                .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                    .Select(member => new FieldError(member, result.ErrorMessage ?? "Validation failed.")))
                .ToArray();

            return Results.Json(new ErrorResponse(
                Code: "validation_error",
                Message: "Request payload failed validation.",
                Errors: fieldErrors,
                TraceId: context.HttpContext.TraceIdentifier
            ), statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}

public static class ErrorResults
{
    public static IResult ValidationFailed(HttpContext context, IEnumerable<FieldError> errors) =>
        Results.Json(new ErrorResponse(
            Code: "validation_error",
            Message: "Request payload failed validation.",
            Errors: errors.ToArray(),
            TraceId: context.TraceIdentifier
        ), statusCode: StatusCodes.Status400BadRequest);

    public static IResult InternalServerError(HttpContext context, string? message = null) =>
        Results.Json(new ErrorResponse(
            Code: "internal_error",
            Message: message ?? "Unexpected server error occurred.",
            TraceId: context.TraceIdentifier
        ), statusCode: StatusCodes.Status500InternalServerError);
}
