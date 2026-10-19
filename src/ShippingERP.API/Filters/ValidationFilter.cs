using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ShippingERP.Application.Common.Exceptions;

namespace ShippingERP.API.Filters;

/// <summary>
/// Runs the FluentValidation validator (if one exists) for every action argument before the action executes.
/// On failure it throws RequestValidationException, which ExceptionHandlingMiddleware turns into
/// HTTP 400 with field-level errors. Controllers therefore never need to call validators themselves.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validator = FindValidator(context.HttpContext.RequestServices, argument.GetType());
            if (validator is null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new RequestValidationException(failures);
        }

        await next();
    }

    /// <summary>
    /// The validator of the argument type, or of its closest base type: multipart forms such as
    /// DeliverShipmentForm (API layer, with IFormFile properties) reuse the validator of the Application request
    /// they inherit (DeliverShipmentRequest).
    /// </summary>
    private static IValidator? FindValidator(IServiceProvider services, Type? type)
    {
        while (type is not null && type != typeof(object))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(type);
            if (services.GetService(validatorType) is IValidator validator)
            {
                return validator;
            }

            type = type.BaseType;
        }

        return null;
    }
}
