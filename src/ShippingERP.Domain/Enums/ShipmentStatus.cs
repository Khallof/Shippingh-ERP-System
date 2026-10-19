namespace ShippingERP.Domain.Enums;

/// <summary>
/// Life cycle of a shipment. Stored as text (e.g. "InTransit"), so never rename a value; add new ones at the end.
/// Which change is allowed from which status is decided by <see cref="Shipments.ShipmentStatusRules"/>.
/// </summary>
public enum ShipmentStatus
{
    /// <summary>تم الإنشاء: registered, not yet received from the sender.</summary>
    Created = 1,

    /// <summary>تم الاستلام من المرسل: a driver picked it up from the sender.</summary>
    PickedUp = 2,

    /// <summary>وصلت إلى الفرع: received at a branch counter.</summary>
    ReceivedAtBranch = 3,

    /// <summary>في المستودع: stored in a warehouse of the current branch.</summary>
    InWarehouse = 4,

    /// <summary>قيد النقل: on its way between two branches (branch transfer).</summary>
    InTransit = 5,

    /// <summary>وصلت إلى فرع الوجهة: received by the destination branch.</summary>
    ArrivedAtDestination = 6,

    /// <summary>خرجت للتوصيل: with a driver (driver delivery manifest).</summary>
    OutForDelivery = 7,

    /// <summary>تم التسليم: delivered with a proof of delivery.</summary>
    Delivered = 8,

    /// <summary>فشل التسليم: the delivery attempt failed (recipient absent, refused, wrong address...).</summary>
    DeliveryFailed = 9,

    /// <summary>أعيدت إلى المرسل: the return workflow is complete.</summary>
    Returned = 10,

    /// <summary>ملغاة: cancelled before it left the origin branch.</summary>
    Cancelled = 11,

    /// <summary>مفقودة أو تالفة: a damage / loss case was opened.</summary>
    LostOrDamaged = 12
}
