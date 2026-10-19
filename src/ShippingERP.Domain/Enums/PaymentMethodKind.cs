namespace ShippingERP.Domain.Enums;

/// <summary>How the money is actually paid. Stored as text.</summary>
public enum PaymentMethodKind
{
    Cash = 1,
    CashOnDelivery = 2,
    BankTransfer = 3,
    Card = 4,
    CustomerAccount = 5,
    Wallet = 6
}
