using Microsoft.AspNetCore.Mvc;
using OrderService.Domain.Common;

namespace OrderService.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return BuildProblem(controller, result.Error!);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value!);
        }

        return BuildProblem(controller, result.Error!);
    }

    private static IActionResult BuildProblem(ControllerBase controller, Error error)
    {
        var status = MapStatus(error.Code);
        return controller.Problem(
            detail: error.Message,
            statusCode: status,
            title: error.Code);
    }

    private static int MapStatus(string code) => code switch
    {
        "validation_error" => StatusCodes.Status400BadRequest,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        "forbidden" => StatusCodes.Status403Forbidden,
        "invalid_credentials" => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError,
    };
}
