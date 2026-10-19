using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Shipments;

/// <summary>Arabic display names for shipment statuses and document statuses (used in DTOs, labels and messages).</summary>
public static class ShipmentStatusNames
{
    public static string Arabic(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Created => "تم الإنشاء",
        ShipmentStatus.PickedUp => "تم الاستلام من المرسل",
        ShipmentStatus.ReceivedAtBranch => "وصلت إلى الفرع",
        ShipmentStatus.InWarehouse => "في المستودع",
        ShipmentStatus.InTransit => "قيد النقل بين الفروع",
        ShipmentStatus.ArrivedAtDestination => "وصلت إلى فرع الوجهة",
        ShipmentStatus.OutForDelivery => "خرجت للتوصيل",
        ShipmentStatus.Delivered => "تم التسليم",
        ShipmentStatus.DeliveryFailed => "فشل التسليم",
        ShipmentStatus.Returned => "أعيدت إلى المرسل",
        ShipmentStatus.Cancelled => "ملغاة",
        ShipmentStatus.LostOrDamaged => "مفقودة أو تالفة",
        _ => status.ToString()
    };
}
