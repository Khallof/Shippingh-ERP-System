namespace ShippingERP.Application.DTOs.Roles;

/// <summary>POST /api/v1/roles (custom role, in addition to the seven built-in ones).</summary>
public sealed class CreateRoleRequest
{
    /// <summary>Latin letters/digits, starts with a letter, e.g. "SeniorAccountant".</summary>
    public string Name { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();
}
