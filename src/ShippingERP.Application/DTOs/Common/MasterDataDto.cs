namespace ShippingERP.Application.DTOs.Common;

/// <summary>Fields returned by every master-data DTO.</summary>
public abstract class MasterDataDto
{
    public long Id { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public long? CreatedByUserId { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public long? UpdatedByUserId { get; init; }
}
