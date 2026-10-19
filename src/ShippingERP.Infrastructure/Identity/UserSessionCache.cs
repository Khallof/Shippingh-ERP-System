using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Caches (IsActive, SecurityStamp) per user for a few seconds so the JWT check on every request
/// does not query the database each time.
/// </summary>
public sealed class UserSessionCache : IUserSessionCache
{
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _options;

    public UserSessionCache(IMemoryCache cache, AppDbContext dbContext, IOptions<JwtOptions> options)
    {
        _cache = cache;
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<UserSessionState?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var key = CacheKey(userId);

        if (_cache.TryGetValue(key, out UserSessionState? cached) && cached is not null)
        {
            return cached;
        }

        var state = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserSessionState(user.Id, user.IsActive, user.SecurityStamp ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);

        if (state is not null && _options.UserStateCacheSeconds > 0)
        {
            _cache.Set(key, state, TimeSpan.FromSeconds(_options.UserStateCacheSeconds));
        }

        return state;
    }

    public void Invalidate(long userId)
    {
        _cache.Remove(CacheKey(userId));
    }

    private static string CacheKey(long userId) => $"user-session:{userId}";
}
