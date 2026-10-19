using System.Collections.Concurrent;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces.Identity;

namespace ShippingERP.Tests.Common;

/// <summary>
/// In-memory replacement for the user state check. Every user is active with stamp
/// <see cref="DefaultSecurityStamp"/> unless a test calls <see cref="Set"/>.
/// </summary>
public sealed class FakeUserSessionCache : IUserSessionCache
{
    public const string DefaultSecurityStamp = "test-security-stamp";

    private readonly ConcurrentDictionary<long, UserSessionState> _states = new();

    public void Set(long userId, bool isActive, string securityStamp) =>
        _states[userId] = new UserSessionState(userId, isActive, securityStamp);

    public Task<UserSessionState?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var state = _states.TryGetValue(userId, out var stored)
            ? stored
            : new UserSessionState(userId, true, DefaultSecurityStamp);

        return Task.FromResult<UserSessionState?>(state);
    }

    public void Invalidate(long userId)
    {
    }
}
