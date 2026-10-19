using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Enums;
using R = ShippingERP.Application.IntegrationEvents.AccountRoles;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Builds the proposed journal entries (AccountingPostingRequest) of the step-6 money movements. Pure functions:
/// the unit tests check that each one is balanced and uses the right account roles. The general ledger itself is
/// written by the accounting module (step 7), which maps the roles to accounts with the posting rules.
/// </summary>
public static class CashPostings
{
    /// <summary>COD collected: the money now belongs to the sender and sits with the driver (or in a cashbox).</summary>
    public static AccountingPostingRequest CodCollected(
        long codId, string shipmentNumber, long currencyId, DateTime dateUtc, decimal amount,
        long senderCustomerId, long branchId, long? driverId, long? cashboxId)
    {
        var holder = cashboxId.HasValue
            ? new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = branchId, CashboxId = cashboxId }
            : new PostingLine { AccountRole = R.DriverCustody, Debit = amount, BranchId = branchId, DriverId = driverId };

        return AccountingPostingRequest.Create(
            CashLedger.CodDocument, codId, shipmentNumber, currencyId, dateUtc,
            $"تحصيل مبلغ الدفع عند الاستلام للشحنة {shipmentNumber}",
            new[]
            {
                holder,
                new PostingLine { AccountRole = R.CodPayableToSenders, Credit = amount, BranchId = branchId, CustomerId = senderCustomerId }
            });
    }

    /// <summary>Collection corrected (delta &gt; 0: the driver holds more) or reversed (delta = -collected).</summary>
    public static AccountingPostingRequest CodCollectionAdjusted(
        long adjustmentId, string adjustmentNumber, long currencyId, DateTime dateUtc, decimal delta,
        long senderCustomerId, long branchId, long driverId)
    {
        return AccountingPostingRequest.Create(
            CashLedger.AdjustmentDocument, adjustmentId, adjustmentNumber, currencyId, dateUtc,
            $"تسوية مبلغ محصّل {adjustmentNumber}",
            new[]
            {
                new PostingLine { AccountRole = R.DriverCustody, Debit = delta, BranchId = branchId, DriverId = driverId },
                new PostingLine { AccountRole = R.CodPayableToSenders, Credit = delta, BranchId = branchId, CustomerId = senderCustomerId }
            });
    }

    /// <summary>Driver handover: cash moves from the driver's custody into the cashbox.</summary>
    public static AccountingPostingRequest DriverHandover(
        long voucherId, string voucherNumber, long currencyId, DateTime dateUtc, decimal amount,
        long branchId, long cashboxId, long driverId)
    {
        return AccountingPostingRequest.Create(
            CashLedger.ReceiptDocument, voucherId, voucherNumber, currencyId, dateUtc,
            $"استلام عهدة سائق بالسند {voucherNumber}",
            new[]
            {
                new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = branchId, CashboxId = cashboxId },
                new PostingLine { AccountRole = R.DriverCustody, Credit = amount, BranchId = branchId, DriverId = driverId }
            });
    }

    /// <summary>Driver shortage written off (credit custody) or excess recorded (debit custody).</summary>
    public static AccountingPostingRequest DriverCashAdjusted(
        long adjustmentId, string adjustmentNumber, CODAdjustmentType type, long currencyId, DateTime dateUtc,
        decimal amount, long branchId, long driverId)
    {
        var custody = new PostingLine { AccountRole = R.DriverCustody, BranchId = branchId, DriverId = driverId };
        var lines = type == CODAdjustmentType.DriverShortageWriteOff
            ? new[]
            {
                new PostingLine { AccountRole = R.DriverShortageLoss, Debit = amount, BranchId = branchId, DriverId = driverId },
                custody with { Credit = amount }
            }
            : new[]
            {
                custody with { Debit = amount },
                new PostingLine { AccountRole = R.CashOverShort, Credit = amount, BranchId = branchId }
            };

        return AccountingPostingRequest.Create(
            CashLedger.AdjustmentDocument, adjustmentId, adjustmentNumber, currencyId, dateUtc,
            $"تسوية عهدة سائق {adjustmentNumber}", lines);
    }

    /// <summary>Manual cash receipt voucher.</summary>
    public static AccountingPostingRequest CashReceipt(
        long voucherId, string voucherNumber, CashReceiptType type, long currencyId, DateTime dateUtc, decimal amount,
        long branchId, long cashboxId, long? customerId)
    {
        var source = type switch
        {
            CashReceiptType.CustomerPayment => new PostingLine { AccountRole = R.CashClearing, Credit = amount, BranchId = branchId, CustomerId = customerId },
            CashReceiptType.OtherIncome => new PostingLine { AccountRole = R.OtherIncome, Credit = amount, BranchId = branchId },
            _ => new PostingLine { AccountRole = R.Suspense, Credit = amount, BranchId = branchId, CustomerId = customerId }
        };

        return AccountingPostingRequest.Create(
            CashLedger.ReceiptDocument, voucherId, voucherNumber, currencyId, dateUtc,
            $"سند قبض نقدي {voucherNumber}",
            new[] { new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = branchId, CashboxId = cashboxId }, source });
    }

    /// <summary>Manual cash payment voucher.</summary>
    public static AccountingPostingRequest CashPayment(
        long voucherId, string voucherNumber, CashPaymentType type, long currencyId, DateTime dateUtc, decimal amount,
        long branchId, long cashboxId, long? expenseCategoryId, long? supplierId, long? employeeId, long? customerId)
    {
        var target = type switch
        {
            CashPaymentType.Expense => new PostingLine { AccountRole = R.Expense, Debit = amount, BranchId = branchId, ExpenseCategoryId = expenseCategoryId },
            CashPaymentType.SupplierPayment => new PostingLine { AccountRole = R.AccountsPayable, Debit = amount, BranchId = branchId, SupplierId = supplierId },
            CashPaymentType.EmployeeAdvance => new PostingLine { AccountRole = R.EmployeeAdvances, Debit = amount, BranchId = branchId, EmployeeId = employeeId },
            CashPaymentType.CustomerRefund => new PostingLine { AccountRole = R.CustomerRefund, Debit = amount, BranchId = branchId, CustomerId = customerId },
            _ => new PostingLine { AccountRole = R.Suspense, Debit = amount, BranchId = branchId }
        };

        return AccountingPostingRequest.Create(
            CashLedger.PaymentDocument, voucherId, voucherNumber, currencyId, dateUtc,
            $"سند صرف نقدي {voucherNumber}",
            new[] { target, new PostingLine { AccountRole = R.CashOnHand, Credit = amount, BranchId = branchId, CashboxId = cashboxId } });
    }

    /// <summary>Sender settlement paid: release the sender money, recognize the deductions, pay the net.</summary>
    public static AccountingPostingRequest SettlementPaid(
        long settlementId, string settlementNumber, long currencyId, DateTime dateUtc, long branchId, long senderCustomerId,
        decimal grossReleased, decimal shipping, decimal commission, decimal tax, decimal otherFees, decimal compensation,
        decimal netPaid, long? cashboxId, long? bankAccountId)
    {
        var payment = cashboxId.HasValue
            ? new PostingLine { AccountRole = R.CashOnHand, Credit = netPaid, BranchId = branchId, CashboxId = cashboxId }
            : new PostingLine { AccountRole = R.Bank, Credit = netPaid, BranchId = branchId, BankAccountId = bankAccountId };

        return AccountingPostingRequest.Create(
            CashLedger.SettlementDocument, settlementId, settlementNumber, currencyId, dateUtc,
            $"تسوية مبالغ التحصيل مع المرسل {settlementNumber}",
            new[]
            {
                new PostingLine { AccountRole = R.CodPayableToSenders, Debit = grossReleased, BranchId = branchId, CustomerId = senderCustomerId },
                new PostingLine { AccountRole = R.SenderCompensationExpense, Debit = compensation, BranchId = branchId, CustomerId = senderCustomerId },
                new PostingLine { AccountRole = R.ShippingRevenue, Credit = shipping, BranchId = branchId, CustomerId = senderCustomerId },
                new PostingLine { AccountRole = R.CodCommissionRevenue, Credit = commission, BranchId = branchId, CustomerId = senderCustomerId },
                new PostingLine { AccountRole = R.TaxPayable, Credit = tax, BranchId = branchId },
                new PostingLine { AccountRole = R.OtherFeesRevenue, Credit = otherFees, BranchId = branchId, CustomerId = senderCustomerId },
                payment
            });
    }

    /// <summary>Approved transfer: money leaves the source cashbox.</summary>
    public static AccountingPostingRequest TransferApproved(
        long transferId, string transferNumber, CashTransferType type, long currencyId, DateTime dateUtc, decimal amount,
        long fromBranchId, long fromCashboxId, long? toBranchId, long? toCashboxId, long? toBankAccountId)
    {
        var destination = type switch
        {
            CashTransferType.BetweenCashboxes => new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = toBranchId, CashboxId = toCashboxId },
            CashTransferType.BetweenBranches => new PostingLine { AccountRole = R.CashInTransit, Debit = amount, BranchId = fromBranchId },
            _ => new PostingLine { AccountRole = R.Bank, Debit = amount, BranchId = fromBranchId, BankAccountId = toBankAccountId }
        };

        return AccountingPostingRequest.Create(
            CashLedger.TransferDocument, transferId, transferNumber, currencyId, dateUtc,
            $"تحويل نقدية {transferNumber}",
            new[] { destination, new PostingLine { AccountRole = R.CashOnHand, Credit = amount, BranchId = fromBranchId, CashboxId = fromCashboxId } });
    }

    /// <summary>Inter-branch transfer received at the destination.</summary>
    public static AccountingPostingRequest TransferReceived(
        long transferId, string transferNumber, long currencyId, DateTime dateUtc, decimal amount,
        long fromBranchId, long toBranchId, long toCashboxId)
    {
        return AccountingPostingRequest.Create(
            CashLedger.TransferDocument, transferId, transferNumber, currencyId, dateUtc,
            $"استلام تحويل نقدية {transferNumber}",
            new[]
            {
                new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = toBranchId, CashboxId = toCashboxId },
                new PostingLine { AccountRole = R.CashInTransit, Credit = amount, BranchId = fromBranchId }
            });
    }

    public static AccountingPostingRequest OpeningBalance(
        long cashboxId, string cashboxCode, long currencyId, DateTime dateUtc, decimal amount, long branchId)
    {
        return AccountingPostingRequest.Create(
            CashLedger.CashboxDocument, cashboxId, cashboxCode, currencyId, dateUtc,
            $"رصيد افتتاحي للصندوق {cashboxCode}",
            new[]
            {
                new PostingLine { AccountRole = R.CashOnHand, Debit = amount, BranchId = branchId, CashboxId = cashboxId },
                new PostingLine { AccountRole = R.OpeningBalanceEquity, Credit = amount, BranchId = branchId }
            });
    }

    /// <summary>Approved closing difference: discrepancy &lt; 0 = shortage, &gt; 0 = excess.</summary>
    public static AccountingPostingRequest ClosingDiscrepancy(
        long closingId, string reference, long currencyId, DateTime dateUtc, decimal discrepancy, long branchId, long cashboxId)
    {
        return AccountingPostingRequest.Create(
            CashLedger.ClosingDocument, closingId, reference, currencyId, dateUtc,
            $"فرق الإقفال اليومي {reference}",
            new[]
            {
                new PostingLine { AccountRole = R.CashOnHand, Debit = discrepancy, BranchId = branchId, CashboxId = cashboxId },
                new PostingLine { AccountRole = R.CashOverShort, Credit = discrepancy, BranchId = branchId }
            });
    }
}
