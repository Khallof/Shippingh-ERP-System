using FluentValidation.Results;
using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Thrown when input validation fails. Mapped to HTTP 400 with field-level details.
/// Named RequestValidationException (not ValidationException) to avoid a name clash with FluentValidation.
/// </summary>
public sealed class RequestValidationException : AppException
{
    public const string DefaultMessage = "البيانات المدخلة غير صحيحة.";

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(ErrorCodes.ValidationError, DefaultMessage)
    {
        Errors = errors;
    }

    public RequestValidationException(IEnumerable<ValidationFailure> failures)
        : this(failures
            .GroupBy(f => f.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase))
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
