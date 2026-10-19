namespace ShippingERP.Domain.Authorization;

/// <summary>One built-in role with the permissions it receives when it is first created.</summary>
public sealed record RoleDefinition(
    string Name,
    string ArabicName,
    string Description,
    IReadOnlyList<string> DefaultPermissions);
