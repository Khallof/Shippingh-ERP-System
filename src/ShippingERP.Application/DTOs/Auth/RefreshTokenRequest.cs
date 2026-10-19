namespace ShippingERP.Application.DTOs.Auth;

/// <summary>POST /api/v1/auth/refresh-token</summary>
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
