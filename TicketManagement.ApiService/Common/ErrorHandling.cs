using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TicketManagement.Contracts.DTOs;

namespace TicketManagement.ApiService.Common;

/// <summary>
/// Centralized error handling and response generation
/// </summary>
public static class ErrorHandler
{
    /// <summary>
    /// Handles exceptions and returns appropriate API responses
    /// </summary>
    public static ActionResult<ApiResponseDto<T>> HandleException<T>(
        Exception ex, 
        ILogger logger, 
        string context)
    {
        return ex switch
        {
            ArgumentException argEx => HandleArgumentException<T>(argEx, logger, context),
            UnauthorizedAccessException unauthEx => HandleUnauthorizedException<T>(unauthEx, logger, context),
            InvalidOperationException invOpEx => HandleInvalidOperationException<T>(invOpEx, logger, context),
            NotFoundException notFoundEx => HandleNotFoundException<T>(notFoundEx, logger, context),
            ValidationException validationEx => HandleValidationException<T>(validationEx, logger, context),
            SecurityException secEx => HandleSecurityException<T>(secEx, logger, context),
            _ => HandleGenericException<T>(ex, logger, context)
        };
    }

    private static ActionResult<ApiResponseDto<T>> HandleArgumentException<T>(
        ArgumentException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogWarning(ex, "Invalid argument in {Context}: {Message}", context, ex.Message);
        return new BadRequestObjectResult(ApiResponseDto<T>.ErrorResult(ex.Message));
    }

    private static ActionResult<ApiResponseDto<T>> HandleUnauthorizedException<T>(
        UnauthorizedAccessException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogWarning(ex, "Unauthorized access in {Context}: {Message}", context, ex.Message);
        return new UnauthorizedObjectResult(ApiResponseDto<T>.ErrorResult("Access denied"));
    }

    private static ActionResult<ApiResponseDto<T>> HandleInvalidOperationException<T>(
        InvalidOperationException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogWarning(ex, "Invalid operation in {Context}: {Message}", context, ex.Message);
        return new BadRequestObjectResult(ApiResponseDto<T>.ErrorResult(ex.Message));
    }

    private static ActionResult<ApiResponseDto<T>> HandleNotFoundException<T>(
        NotFoundException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogDebug(ex, "Resource not found in {Context}: {Message}", context, ex.Message);
        return new NotFoundObjectResult(ApiResponseDto<T>.ErrorResult(ex.Message));
    }

    private static ActionResult<ApiResponseDto<T>> HandleValidationException<T>(
        ValidationException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogDebug(ex, "Validation failed in {Context}: {Message}", context, ex.Message);
        var errorList = ex.Errors?.ToList() ?? new List<string> { ex.Message };
        return new BadRequestObjectResult(ApiResponseDto<T>.ErrorResult(errorList));
    }

    private static ActionResult<ApiResponseDto<T>> HandleSecurityException<T>(
        SecurityException ex, 
        ILogger logger, 
        string context)
    {
        logger.LogError(ex, "Security violation in {Context}: {Message}", context, ex.Message);
        return new UnauthorizedObjectResult(ApiResponseDto<T>.ErrorResult("Access denied"));
    }

    private static ActionResult<ApiResponseDto<T>> HandleGenericException<T>(
        Exception ex, 
        ILogger logger, 
        string context)
    {
        logger.LogError(ex, "Unexpected error in {Context}: {Message}", context, ex.Message);
        return new ObjectResult(ApiResponseDto<T>.ErrorResult("An unexpected error occurred"))
        {
            StatusCode = 500
        };
    }
}

/// <summary>
/// Custom exception types for better error categorization
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

public class ValidationException : Exception
{
    public string[]? Errors { get; }

    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, string[] errors) : base(message) 
    {
        Errors = errors;
    }
    public ValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
    public SecurityException(string message, Exception innerException) : base(message, innerException) { }
}