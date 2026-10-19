namespace ShippingERP.Infrastructure.Accounting;

/// <summary>Settings of the accounting module (appsettings section "Accounting").</summary>
public sealed class AccountingOptions
{
    public const string SectionName = "Accounting";

    /// <summary>
    /// False (default, recommended): a journal entry is approved by a user other than its author (four eyes).
    /// True only for a one-person test installation.
    /// </summary>
    public bool AllowSelfApproval { get; set; }

    /// <summary>Run the automatic posting of the operational events in the background.</summary>
    public bool DispatcherEnabled { get; set; } = true;

    /// <summary>Seconds between two dispatcher runs (default 10).</summary>
    public int DispatcherIntervalSeconds { get; set; } = 10;

    /// <summary>Outbox messages per dispatcher run (default 100).</summary>
    public int DispatcherBatchSize { get; set; } = 100;

    /// <summary>Delay after start-up before the first run (default 15 seconds).</summary>
    public int DispatcherStartDelaySeconds { get; set; } = 15;

    /// <summary>
    /// After this many failed attempts the dispatcher stops retrying a message automatically; it stays Failed until
    /// an accountant retries it (POST /accounting/posting-events/{id}/retry).
    /// </summary>
    public int MaxAutomaticAttempts { get; set; } = 5;

    /// <summary>
    /// Account number of the retained earnings (الأرباح المحتجزة). The trial balance shows the profit of the earlier
    /// fiscal years on it (the profit and loss accounts start every year at zero).
    /// </summary>
    public string RetainedEarningsAccountCode { get; set; } = "3200";
}
