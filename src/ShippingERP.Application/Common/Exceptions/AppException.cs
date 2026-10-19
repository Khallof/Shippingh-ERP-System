namespace ShippingERP.Application.Common.Exceptions;

/// <summary>
/// Base class for all expected (business) exceptions.
/// The global ExceptionHandlingMiddleware turns each subclass into the right HTTP status code
/// and the standard ApiResponse error format. Messages are shown to the user, so write them in Arabic.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Stable error code from ErrorCodes.</summary>
    public string ErrorCode { get; }
}
