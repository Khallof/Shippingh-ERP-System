using ShippingERP.Application.Common.Exceptions;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Collects field errors so the client gets all of them at once (400 VALIDATION_ERROR).</summary>
internal sealed class ErrorBag
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public bool HasErrors => _errors.Count > 0;

    public void Add(string fieldName, string message)
    {
        if (!_errors.TryGetValue(fieldName, out var messages))
        {
            messages = new List<string>();
            _errors[fieldName] = messages;
        }

        if (!messages.Contains(message))
        {
            messages.Add(message);
        }
    }

    public void ThrowIfAny()
    {
        if (HasErrors)
        {
            throw ToException();
        }
    }

    public RequestValidationException ToException() =>
        new(_errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
}
