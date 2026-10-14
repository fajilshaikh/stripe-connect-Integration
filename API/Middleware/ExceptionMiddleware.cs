using System.Text.Json;
using API.Errors;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace API.Middleware;

public class ExceptionMiddleware(IHostEnvironment env, RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            StripeException e => (StatusCodes.Status402PaymentRequired, e.StripeError?.Message ?? e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record was changed by another request (e.g. a webhook). Refresh and try again."),
            InvalidOperationException => (StatusCodes.Status409Conflict, ex.Message),   // business rule (wrong status, bad amount)
            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, ex.Message),
        };

        string? details = null;
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            details = env.IsDevelopment() ? ex.StackTrace : "Internal server error";
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ApiErrorResponse(statusCode, message, details);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
