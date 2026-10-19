using Microsoft.AspNetCore.Identity;
using ShippingERP.Application.Common.Exceptions;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Converts a failed IdentityResult into the API's standard exceptions:
/// duplicates -> 409 CONFLICT, everything else -> 400 VALIDATION_ERROR with the field name.
/// </summary>
public static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result, string passwordField = "password")
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = result.Errors.ToList();

        var duplicate = errors.FirstOrDefault(error =>
            error.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                or nameof(IdentityErrorDescriber.DuplicateEmail)
                or nameof(IdentityErrorDescriber.DuplicateRoleName));

        if (duplicate is not null)
        {
            throw new ConflictException(duplicate.Description);
        }

        if (errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            throw new ConflictException(errors.First().Description);
        }

        var details = errors
            .GroupBy(error => FieldFor(error.Code, passwordField))
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).Distinct().ToArray());

        throw new RequestValidationException(details);
    }

    private static string FieldFor(string code, string passwordField)
    {
        if (code.StartsWith("Password", StringComparison.Ordinal))
        {
            return passwordField;
        }

        return code switch
        {
            nameof(IdentityErrorDescriber.InvalidUserName) => "userName",
            nameof(IdentityErrorDescriber.InvalidEmail) => "email",
            nameof(IdentityErrorDescriber.InvalidRoleName) => "name",
            nameof(IdentityErrorDescriber.UserAlreadyInRole) or nameof(IdentityErrorDescriber.UserNotInRole) => "roles",
            _ => "request"
        };
    }
}
