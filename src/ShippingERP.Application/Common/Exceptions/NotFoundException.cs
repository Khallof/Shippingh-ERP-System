using ShippingERP.Application.Common.Models;

namespace ShippingERP.Application.Common.Exceptions;

/// <summary>Thrown when a requested record does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(ErrorCodes.NotFound, message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base(ErrorCodes.NotFound, $"لم يتم العثور على {entityName} بالمعرف ({key}).")
    {
    }
}
