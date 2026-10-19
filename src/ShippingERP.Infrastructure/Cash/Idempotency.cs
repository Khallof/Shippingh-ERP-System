using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShippingERP.Application.Common.Exceptions;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Idempotency-Key handling shared by the money POSTs of step 6 (driver handover, cash receipts, cash payments),
/// the same rules as the customer payments of step 5:
///   same key + same body (same user)  -> the first document is returned, nothing is recorded again;
///   same key + different body / user  -> 409;
///   two identical requests at the same moment -> the unique index on the key lets only one be saved.
/// </summary>
public static class Idempotency
{
    public const int MaxKeyLength = 100;

    private static readonly JsonSerializerOptions HashJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Trimmed key, null when absent. 400 when too long.</summary>
    public static string? NormalizeKey(string? idempotencyKey)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key is not null && key.Length > MaxKeyLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = new[] { $"مفتاح Idempotency-Key يجب ألا يزيد عن {MaxKeyLength} حرفاً." }
            });
        }

        return key;
    }

    /// <summary>SHA-256 (hex) of the request body serialized as camelCase JSON.</summary>
    public static string Hash<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, HashJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>409 when an existing document with the key was made by another request or another user.</summary>
    public static void EnsureSameRequest(string storedHash, long? storedUserId, string hash, long? currentUserId)
    {
        if (storedHash != hash || storedUserId != currentUserId)
        {
            throw new ConflictException("مفتاح Idempotency-Key مستخدم من قبل لطلب مختلف. استخدم مفتاحاً جديداً لكل عملية جديدة.");
        }
    }
}
