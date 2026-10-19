namespace ShippingERP.Domain.Entities.Identity;

/// <summary>Values stored in RefreshToken.RevokedReason.</summary>
public static class RefreshTokenRevokeReasons
{
    public const string Rotated = "Rotated";
    public const string Logout = "Logout";
    public const string PasswordChanged = "PasswordChanged";
    public const string UserDeactivated = "UserDeactivated";
    public const string ReuseDetected = "ReuseDetected";
}
