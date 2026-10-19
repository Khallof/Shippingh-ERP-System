using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Tracking numbers: prefix + 10 random digits + 1 check digit, e.g. "SE48213907651".
///
/// - Random (cryptographic generator), so numbers cannot be guessed by counting: safe to give to customers.
/// - The last digit is a Luhn check digit: a mistyped digit is detected (<see cref="HasValidCheckDigit"/>).
/// - Uniqueness is checked against the database (and the unique index is the final guarantee).
/// </summary>
public sealed class TrackingNumberGenerator
{
    public const int RandomDigits = 10;
    private const int MaxAttempts = 20;

    private readonly AppDbContext _dbContext;

    public TrackingNumberGenerator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <param name="prefix">From the shipping settings, e.g. "SE".</param>
    /// <param name="reserved">Numbers already given in the current batch (Excel import) but not saved yet.</param>
    public async Task<string> NextAsync(string prefix, ISet<string>? reserved, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Create(prefix);

            if (reserved?.Contains(candidate) == true)
            {
                continue;
            }

            if (!await _dbContext.Shipments.AnyAsync(x => x.TrackingNumber == candidate, cancellationToken))
            {
                reserved?.Add(candidate);
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique tracking number.");
    }

    public static string Create(string prefix)
    {
        var builder = new StringBuilder(prefix.Length + RandomDigits + 1);
        builder.Append(prefix);

        var digits = new char[RandomDigits];
        for (var i = 0; i < RandomDigits; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        builder.Append(digits);
        builder.Append(LuhnCheckDigit(new string(digits)));
        return builder.ToString();
    }

    /// <summary>True when the last digit matches the Luhn check digit of the digits before it.</summary>
    public static bool HasValidCheckDigit(string trackingNumber)
    {
        var digits = new string(trackingNumber.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 2)
        {
            return false;
        }

        return LuhnCheckDigit(digits[..^1]) == digits[^1];
    }

    private static char LuhnCheckDigit(string digits)
    {
        var sum = 0;
        var doubleIt = true;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (doubleIt)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleIt = !doubleIt;
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }
}
