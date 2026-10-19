namespace ShippingERP.Domain.Enums;

/// <summary>
/// What the customer does in shipments. Flags: a customer can be both (Sender | Recipient = 3).
/// Stored as a number.
/// </summary>
[Flags]
public enum CustomerRole
{
    Sender = 1,
    Recipient = 2,
    Both = Sender | Recipient
}
