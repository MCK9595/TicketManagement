using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TicketManagement.ApiService.Filters;

/// <summary>
/// モデル検証エラーを統一されたレスポンス形式で返すフィルター
/// </summary>
public class ModelValidationFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            var result = new
            {
                Type = "ValidationError",
                Title = "One or more validation errors occurred.",
                Status = 400,
                Errors = errors
            };

            context.Result = new BadRequestObjectResult(result);
        }

        base.OnActionExecuting(context);
    }
}

/// <summary>
/// APIレスポンスを統一形式でラップするフィルター
/// </summary>
public class ApiResponseWrapperFilter : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.StatusCode == 200)
        {
            var wrappedResponse = new
            {
                Success = true,
                Data = objectResult.Value,
                Message = "Operation completed successfully"
            };

            context.Result = new OkObjectResult(wrappedResponse);
        }

        base.OnActionExecuted(context);
    }
}