using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Entities.Integration;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Integration;

/// <summary>
/// Saves integration events in the transactional outbox (OutboxMessages). It only ADDS the row: the caller's
/// SaveChanges stores it in the same transaction as the business change, so the event exists exactly when the
/// change was committed. A dispatcher (next phase, accounting) delivers the unprocessed rows.
/// </summary>
public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUser;

    public OutboxIntegrationEventPublisher(AppDbContext dbContext, IDateTimeProvider dateTimeProvider, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUser = currentUser;
    }

    public void Publish(IntegrationEvent integrationEvent)
    {
        var occurredAt = integrationEvent.OccurredAtUtc == default ? _dateTimeProvider.UtcNow : integrationEvent.OccurredAtUtc;
        var stamped = integrationEvent with { OccurredAtUtc = occurredAt, UserId = integrationEvent.UserId ?? _currentUser.UserId };

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventId = stamped.EventId,
            EventType = stamped.EventType,
            PayloadJson = JsonSerializer.Serialize(stamped, stamped.GetType(), JsonOptions),
            OccurredAtUtc = occurredAt
        });
    }
}
