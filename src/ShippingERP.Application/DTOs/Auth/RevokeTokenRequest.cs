namespace ShippingERP.Application.DTOs.Auth;

/// <summary>POST /api/v1/auth/revoke-token (sign out from this device).</summary>
public sealed class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
