namespace ShippingERP.Domain.Common;

/// <summary>
/// Marks an entity that can only be INSERTED, never updated or deleted
/// (for example: journal entries, journal lines, COD collection transactions,
/// shipment tracking events, audit logs).
///
/// Mistakes in these records are corrected by inserting a new reversing record,
/// never by editing or deleting the original. The AuditableEntityInterceptor
/// throws an exception if code tries to modify or delete such an entity.
/// </summary>
public interface IAppendOnlyEntity
{
}
