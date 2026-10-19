using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Billing;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.MasterData;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>
/// Customer payments (سندات القبض) and their allocation to invoices (step 5).
///
/// One transaction = payment + allocations + invoice balances / statuses + sub-ledger credit + audit + outbox.
/// Concurrency: Invoice.RowVersion makes a second payment that changed the same invoice at the same moment fail
/// with 409 CONCURRENCY_CONFLICT instead of over-paying the invoice.
/// Idempotency: see <see cref="CreateAsync"/>.
/// </summary>
public sealed class CustomerPaymentService : ICustomerPaymentService
{
    private const string EntityName = "CustomerPayment";
    public const int MaxIdempotencyKeyLength = 100;

    private static readonly Dictionary<string, Expression<Func<CustomerPayment, object?>>> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["paymentDateUtc"] = x => x.PaymentDateUtc,
        ["paymentNumber"] = x => x.PaymentNumber,
        ["amount"] = x => x.Amount,
        ["unallocatedAmount"] = x => x.UnallocatedAmount
    };

    private static readonly JsonSerializerOptions HashJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly MasterDataServiceDependencies _dependencies;
    private readonly BillingAccess _access;
    private readonly DocumentNumberGenerator _numbers;
    private readonly CustomerLedger _ledger;
    private readonly IIntegrationEventPublisher _events;

    public CustomerPaymentService(
        MasterDataServiceDependencies dependencies,
        BillingAccess access,
        DocumentNumberGenerator numbers,
        CustomerLedger ledger,
        IIntegrationEventPublisher events)
    {
        _dependencies = dependencies;
        _access = access;
        _numbers = numbers;
        _ledger = ledger;
        _events = events;
    }

    private AppDbContext Db => _dependencies.DbContext;

    private DateTime UtcNow => _dependencies.DateTimeProvider.UtcNow;

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<CustomerPaymentListItemDto>> GetPagedAsync(CustomerPaymentListQuery query, CancellationToken cancellationToken)
    {
        var rows = _access.Payments();

        if (query.CustomerId.HasValue)
        {
            var customerId = query.CustomerId.Value;
            rows = rows.Where(x => x.CustomerId == customerId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.PaymentMethodId.HasValue)
        {
            var methodId = query.PaymentMethodId.Value;
            rows = rows.Where(x => x.PaymentMethodId == methodId);
        }

        if (query.CurrencyId.HasValue)
        {
            var currencyId = query.CurrencyId.Value;
            rows = rows.Where(x => x.CurrencyId == currencyId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            rows = rows.Where(x => x.PaymentDateUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            var to = query.ToUtc.Value;
            rows = rows.Where(x => x.PaymentDateUtc < to);
        }

        if (query.HasUnallocatedAmount.HasValue)
        {
            rows = query.HasUnallocatedAmount.Value ? rows.Where(x => x.UnallocatedAmount > 0m) : rows.Where(x => x.UnallocatedAmount == 0m);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x =>
                x.PaymentNumber.Contains(search) ||
                (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)) ||
                x.Customer.Code.Contains(search) ||
                x.Customer.ArabicName.Contains(search));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<CustomerPayment> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && Sorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderByDescending(x => x.PaymentDateUtc);

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new CustomerPaymentListItemDto
            {
                Id = x.Id,
                PaymentNumber = x.PaymentNumber,
                CustomerId = x.CustomerId,
                CustomerName = x.Customer.ArabicName,
                BranchName = x.Branch.ArabicName,
                PaymentMethodName = x.PaymentMethod.ArabicName,
                CurrencyCode = x.Currency.Code,
                Amount = x.Amount,
                AllocatedAmount = x.AllocatedAmount,
                UnallocatedAmount = x.UnallocatedAmount,
                PaymentDateUtc = x.PaymentDateUtc,
                ReferenceNumber = x.ReferenceNumber
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerPaymentListItemDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<CustomerPaymentDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await _access.Payments()
                   .Where(x => x.Id == id)
                   .Select(x => new CustomerPaymentDto
                   {
                       Id = x.Id,
                       PaymentNumber = x.PaymentNumber,
                       CustomerId = x.CustomerId,
                       CustomerCode = x.Customer.Code,
                       CustomerName = x.Customer.ArabicName,
                       BranchId = x.BranchId,
                       BranchName = x.Branch.ArabicName,
                       PaymentMethodId = x.PaymentMethodId,
                       PaymentMethodName = x.PaymentMethod.ArabicName,
                       CurrencyId = x.CurrencyId,
                       CurrencyCode = x.Currency.Code,
                       Amount = x.Amount,
                       PaymentDateUtc = x.PaymentDateUtc,
                       ReferenceNumber = x.ReferenceNumber,
                       Notes = x.Notes,
                       AllocatedAmount = x.AllocatedAmount,
                       UnallocatedAmount = x.UnallocatedAmount,
                       CreatedAtUtc = x.CreatedAtUtc,
                       CreatedByUserId = x.CreatedByUserId,
                       Allocations = x.Allocations
                           .OrderBy(a => a.Id)
                           .Select(a => new PaymentAllocationDto
                           {
                               Id = a.Id,
                               InvoiceId = a.InvoiceId,
                               InvoiceNumber = a.Invoice.InvoiceNumber,
                               Amount = a.Amount,
                               AllocatedAtUtc = a.AllocatedAtUtc,
                               InvoiceBalanceDue = a.Invoice.BalanceDue
                           })
                           .ToList()
                   })
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("سند القبض", id);
    }

    // ------------------------------------------------------------------ create

    /// <summary>
    /// Idempotency: with a key, the first request records the payment; a repeated request with the same key and
    /// the same body returns that payment (IsReplay = true, nothing is recorded again); the same key with a
    /// different body is refused (409). The unique index on IdempotencyKey also catches two identical requests
    /// arriving at the same moment.
    /// </summary>
    public async Task<CustomerPaymentCreationResult> CreateAsync(
        CreateCustomerPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key is not null && key.Length > MaxIdempotencyKeyLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = new[] { $"مفتاح Idempotency-Key يجب ألا يزيد عن {MaxIdempotencyKeyLength} حرفاً." }
            });
        }

        var hash = Hash(request);
        if (key is not null)
        {
            var replay = await FindReplayAsync(key, hash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        // ---------------- references
        var customer = await Db.Customers.AsNoTracking()
                           .Where(x => x.Id == request.CustomerId)
                           .Select(x => new { x.Id, x.DefaultBranchId })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw Invalid("customerId", "العميل غير موجود.");

        var method = await Db.PaymentMethods.AsNoTracking()
                         .Where(x => x.Id == request.PaymentMethodId)
                         .Select(x => new { x.Id, x.ArabicName, x.Kind, x.IsActive, x.RequiresReference })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw Invalid("paymentMethodId", "طريقة الدفع غير موجودة.");

        if (!method.IsActive)
        {
            throw Invalid("paymentMethodId", "طريقة الدفع غير مفعّلة.");
        }

        if (method.Kind is PaymentMethodKind.CashOnDelivery or PaymentMethodKind.CustomerAccount)
        {
            throw Invalid("paymentMethodId", "هذه الطريقة لا تمثّل دفعة نقدية من العميل. اختر نقداً أو تحويلاً بنكياً أو بطاقة.");
        }

        if (method.RequiresReference && string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            throw Invalid("referenceNumber", "رقم المرجع مطلوب لطريقة الدفع هذه (رقم الحوالة / إيصال البطاقة).");
        }

        var currency = await _access.GetCurrencyAsync(request.CurrencyId, cancellationToken);
        var branchId = await _access.ResolveBranchAsync(request.BranchId, customer.DefaultBranchId, cancellationToken);

        var now = UtcNow;
        var paymentDate = request.PaymentDateUtc ?? now;
        if (paymentDate > now.AddMinutes(5))
        {
            throw Invalid("paymentDateUtc", "تاريخ الدفعة لا يمكن أن يكون في المستقبل.");
        }

        var amount = BillingAccess.Round(request.Amount, currency.DecimalPlaces);
        var allocations = await PlanAllocationsAsync(customer.Id, currency, amount, request.AutoAllocate, request.Allocations, cancellationToken);

        var payment = new CustomerPayment
        {
            CustomerId = customer.Id,
            BranchId = branchId,
            PaymentMethodId = method.Id,
            CurrencyId = currency.Id,
            Amount = amount,
            PaymentDateUtc = paymentDate,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IdempotencyKey = key,
            RequestHash = hash
        };

        long id;
        try
        {
            id = await Db.ExecuteInTransactionAsync(async ct =>
            {
                payment.PaymentNumber = await _numbers.NextAsync(DocumentPrefixes.CustomerPayment, ct);
                ApplyAllocations(payment, allocations, now);

                Db.CustomerPayments.Add(payment);
                await Db.SaveChangesAsync(ct);

                _ledger.PaymentReceived(payment, method.ArabicName);
                _events.Publish(new CustomerPaymentReceivedEvent
                {
                    PaymentId = payment.Id,
                    PaymentNumber = payment.PaymentNumber,
                    CustomerId = payment.CustomerId,
                    BranchId = payment.BranchId,
                    PaymentMethodId = payment.PaymentMethodId,
                    PaymentMethodKind = method.Kind.ToString(),
                    CurrencyId = payment.CurrencyId,
                    Amount = payment.Amount,
                    PaymentDateUtc = payment.PaymentDateUtc,
                    ReferenceNumber = payment.ReferenceNumber,
                    Allocations = allocations.Select(x => new PaymentAllocationLine(x.Invoice.Id, x.Invoice.InvoiceNumber, x.Amount)).ToList()
                });
                _dependencies.AuditLog.Add(
                    AuditAction.CustomerPaymentRecorded,
                    entityName: EntityName,
                    entityId: payment.Id.ToString(CultureInfo.InvariantCulture),
                    details: new
                    {
                        payment.PaymentNumber,
                        payment.CustomerId,
                        payment.Amount,
                        payment.AllocatedAmount,
                        invoices = allocations.Select(x => new { x.Invoice.InvoiceNumber, x.Amount })
                    });

                await Db.SaveChangesAsync(ct);
                return payment.Id;
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (key is not null && BillingAccess.IsUniqueViolation(exception))
        {
            // The same key was saved by a parallel request a moment ago.
            Db.ChangeTracker.Clear();
            return await FindReplayAsync(key, hash, cancellationToken)
                   ?? throw new ConflictException("تعذر تسجيل الدفعة بسبب طلب متزامن. أعد المحاولة.");
        }

        return new CustomerPaymentCreationResult { Payment = await GetByIdAsync(id, cancellationToken), IsReplay = false };
    }

    public async Task<CustomerPaymentDto> AllocateAsync(long paymentId, AllocatePaymentRequest request, CancellationToken cancellationToken)
    {
        var payment = await _access.Payments(tracked: true).FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
                      ?? throw new NotFoundException("سند القبض", paymentId);

        if (payment.UnallocatedAmount <= 0m)
        {
            throw new BusinessRuleException("لا يوجد مبلغ غير مخصص في هذه الدفعة.");
        }

        var currency = await _access.GetCurrencyAsync(payment.CurrencyId, cancellationToken);
        var allocations = await PlanAllocationsAsync(payment.CustomerId, currency, payment.UnallocatedAmount, request.AutoAllocate, request.Allocations, cancellationToken);
        if (allocations.Count == 0)
        {
            throw new BusinessRuleException("لا توجد فواتير مفتوحة لهذا العميل بنفس العملة.");
        }

        var now = UtcNow;
        await Db.ExecuteInTransactionAsync(async ct =>
        {
            ApplyAllocations(payment, allocations, now);

            _events.Publish(new CustomerPaymentAllocatedEvent
            {
                PaymentId = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                CustomerId = payment.CustomerId,
                CurrencyId = payment.CurrencyId,
                Allocations = allocations.Select(x => new PaymentAllocationLine(x.Invoice.Id, x.Invoice.InvoiceNumber, x.Amount)).ToList()
            });
            _dependencies.AuditLog.Add(
                AuditAction.CustomerPaymentAllocated,
                entityName: EntityName,
                entityId: payment.Id.ToString(CultureInfo.InvariantCulture),
                details: new { invoices = allocations.Select(x => new { x.Invoice.InvoiceNumber, x.Amount }), payment.UnallocatedAmount });

            await Db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await GetByIdAsync(paymentId, cancellationToken);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record PlannedAllocation(Invoice Invoice, decimal Amount);

    /// <summary>
    /// Validates the requested allocations (or picks the oldest open invoices for AutoAllocate) and returns the
    /// tracked invoices with the amounts. Nothing is changed yet.
    /// </summary>
    private async Task<List<PlannedAllocation>> PlanAllocationsAsync(
        long customerId,
        CurrencyRef currency,
        decimal available,
        bool autoAllocate,
        IReadOnlyList<PaymentAllocationRequest> requested,
        CancellationToken cancellationToken)
    {
        var result = new List<PlannedAllocation>();

        if (autoAllocate)
        {
            var open = await _access.Invoices(tracked: true)
                .Where(x => x.CustomerId == customerId &&
                            x.CurrencyId == currency.Id &&
                            (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid || x.Status == InvoiceStatus.Overdue) &&
                            x.BalanceDue > 0m)
                .OrderBy(x => x.DueDateUtc)
                .ThenBy(x => x.InvoiceDateUtc)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var left = available;
            foreach (var invoice in open)
            {
                if (left <= 0m)
                {
                    break;
                }

                var amount = Math.Min(left, invoice.BalanceDue);
                result.Add(new PlannedAllocation(invoice, amount));
                left -= amount;
            }

            return result;
        }

        if (requested.Count == 0)
        {
            return result;
        }

        var ids = requested.Select(x => x.InvoiceId).ToList();
        var invoices = await _access.Invoices(tracked: true).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < requested.Count; i++)
        {
            var item = requested[i];
            var fieldName = $"allocations[{i}]";

            if (!invoices.TryGetValue(item.InvoiceId, out var invoice))
            {
                errors[fieldName + ".invoiceId"] = new[] { $"الفاتورة {item.InvoiceId} غير موجودة." };
                continue;
            }

            var label = invoice.InvoiceNumber ?? $"#{invoice.Id}";
            var amount = BillingAccess.Round(item.Amount, currency.DecimalPlaces);

            if (invoice.CustomerId != customerId)
            {
                errors[fieldName + ".invoiceId"] = new[] { $"الفاتورة {label} تخص عميلاً آخر." };
            }
            else if (invoice.CurrencyId != currency.Id)
            {
                errors[fieldName + ".invoiceId"] = new[] { $"عملة الفاتورة {label} تختلف عن عملة الدفعة." };
            }
            else if (!InvoiceStatusRules.IsOpen(invoice.Status))
            {
                errors[fieldName + ".invoiceId"] = new[] { $"الفاتورة {label} في حالة «{BillingNames.Arabic(invoice.Status)}» ولا تقبل دفعات." };
            }
            else if (amount > invoice.BalanceDue)
            {
                errors[fieldName + ".amount"] = new[] { $"المبلغ أكبر من رصيد الفاتورة {label} ({invoice.BalanceDue})." };
            }
            else
            {
                result.Add(new PlannedAllocation(invoice, amount));
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        if (result.Sum(x => x.Amount) > available)
        {
            throw Invalid("allocations", $"مجموع التخصيصات أكبر من المبلغ المتاح ({available}).");
        }

        return result;
    }

    /// <summary>Adds the allocation rows and updates the invoices and the payment amounts.</summary>
    private static void ApplyAllocations(CustomerPayment payment, IReadOnlyList<PlannedAllocation> allocations, DateTime nowUtc)
    {
        foreach (var allocation in allocations)
        {
            payment.Allocations.Add(new CustomerPaymentAllocation
            {
                InvoiceId = allocation.Invoice.Id,
                Amount = allocation.Amount,
                AllocatedAtUtc = nowUtc
            });

            allocation.Invoice.PaidAmount += allocation.Amount;
            InvoiceStatusRules.Refresh(allocation.Invoice, nowUtc);
        }

        payment.AllocatedAmount += allocations.Sum(x => x.Amount);
        payment.UnallocatedAmount = payment.Amount - payment.AllocatedAmount;
    }

    private async Task<CustomerPaymentCreationResult?> FindReplayAsync(string key, string hash, CancellationToken cancellationToken)
    {
        var existing = await Db.CustomerPayments.AsNoTracking()
            .Where(x => x.IdempotencyKey == key)
            .Select(x => new { x.Id, x.RequestHash, x.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.RequestHash != hash || existing.CreatedByUserId != _dependencies.CurrentUser.UserId)
        {
            throw new ConflictException("مفتاح Idempotency-Key مستخدم من قبل لطلب مختلف. استخدم مفتاحاً جديداً لكل دفعة جديدة.");
        }

        return new CustomerPaymentCreationResult { Payment = await GetByIdAsync(existing.Id, cancellationToken), IsReplay = true };
    }

    /// <summary>SHA-256 of the request body (camelCase JSON, property order of the class) as hex.</summary>
    private static string Hash(CreateCustomerPaymentRequest request)
    {
        var json = JsonSerializer.Serialize(request, HashJsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });
}
