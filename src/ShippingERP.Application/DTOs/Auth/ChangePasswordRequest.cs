namespace ShippingERP.Application.DTOs.Auth;

/// <summary>POST /api/v1/auth/change-password</summary>
public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmNewPassword { get; set; } = string.Empty;
}
