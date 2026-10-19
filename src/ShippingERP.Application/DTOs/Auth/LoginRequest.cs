namespace ShippingERP.Application.DTOs.Auth;

/// <summary>POST /api/v1/auth/login</summary>
public sealed class LoginRequest
{
    /// <summary>User name or e-mail address.</summary>
    public string UserNameOrEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
