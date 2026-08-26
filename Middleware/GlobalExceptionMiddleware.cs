using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using miniDriveBackend.Business.Exceptions;
using UnauthorizedAccessException = miniDriveBackend.Business.Exceptions.UnauthorizedAccessException;

namespace miniDriveBackend.Middleware
{
    // Centralized mapping of business/validation exceptions to consistent HTTP responses.
    // Mirrors the design documented in Docs/Business/03-Exceptions.md.
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning(ex, "Business error: {ErrorCode}", ex.ErrorCode);
                await WriteErrorResponseAsync(context, ex.StatusCode, ex.ErrorCode, ex.Message, GetExceptionDetails(ex));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error");
                await WriteErrorResponseAsync(context, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", ex.Message, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error");
                await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.", null);
            }
        }

        private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string errorCode, string message, object? details)
        {
            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                errorCode,
                message,
                details
            });
        }

        private static object? GetExceptionDetails(BusinessException ex) => ex switch
        {
            QuotaExceededException qe => new { qe.RequestedBytes, qe.AvailableBytes, qe.QuotaBytes },
            DuplicateResourceException dr => new { dr.ResourceType, dr.Field, dr.Value },
            S3OperationException s3 => new { s3.Operation, s3.S3Key },
            UnauthorizedAccessException ua => new { ua.ResourceType, ua.ResourceId },
            _ => null
        };
    }
}
