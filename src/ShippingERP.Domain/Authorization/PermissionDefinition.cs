namespace ShippingERP.Domain.Authorization;

/// <summary>One entry of the permission catalog.</summary>
/// <param name="Code">e.g. "Shipments.Create".</param>
/// <param name="Module">e.g. "Shipments".</param>
/// <param name="Description">Arabic description for the role editor.</param>
public sealed record PermissionDefinition(string Code, string Module, string Description);
