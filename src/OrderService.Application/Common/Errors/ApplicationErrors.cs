using OrderService.Domain.Common;

namespace OrderService.Application.Common.Errors;

public static class ApplicationErrors
{
    public static Error Validation(string message) => new("validation_error", message);
    public static Error NotFound(string resource) => new("not_found", $"{resource} not found.");
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Forbidden() => new("forbidden", "Access denied.");
    public static Error InvalidCredentials() => new("invalid_credentials", "Invalid username or password.");
}
