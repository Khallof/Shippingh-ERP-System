using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Identity;

/// <summary>
/// A long-lived token used to get a new access token without asking for the password again.
///
/// Security rules:
/// - Only the SHA-256 hash of the token is stored. A database leak does not reveal usable tokens.
/// - Each token can be used ONCE. Using it creates a new token (rotation) and links the old one to it
///   through ReplacedByTokenId.
/// - All tokens that descend from one sign-in share a FamilyId. If an already-rotated token is presented
///   again (a sign of theft), the whole family is revoked and the user must sign in again.
/// </summary>
public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    /// <summary>SHA-256 hash of the token (64 hex characters). The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Same value for all tokens created from one sign-in (one device session).</summary>
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>IP address of the client that received this token.</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Browser / app that received this token.</summary>
    public string? UserAgent { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedByIp { get; set; }

    /// <summary>Why it was revoked: Rotated, Logout, PasswordChanged, UserDeactivated, ReuseDetected...</summary>
    public string? RevokedReason { get; set; }

    /// <summary>The token that replaced this one when it was rotated.</summary>
    public long? ReplacedByTokenId { get; set; }

    public RefreshToken? ReplacedByToken { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string? ipAddress, string reason, RefreshToken? replacedBy = null)
    {
        RevokedAtUtc = utcNow;
        RevokedByIp = ipAddress;
        RevokedReason = reason;
        ReplacedByToken = replacedBy;
    }
}
