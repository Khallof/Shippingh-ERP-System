using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Registers ASP.NET Core Identity (users, roles, password hashing, lockout) on top of AppDbContext,
/// plus JWT creation, the identity services and the seeder.
/// Separate from DependencyInjection.cs so unit tests can call it with an in-memory database.
/// </summary>
public static class IdentityServiceRegistration
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SecretKey) &&
                           options.SecretKey.Length >= JwtOptions.MinimumSecretKeyLength,
                $"Jwt:SecretKey is missing or shorter than {JwtOptions.MinimumSecretKeyLength} characters. " +
                "Set the environment variable Jwt__SecretKey (see the step 2 guide).")
            .Validate(
                options => options.AccessTokenMinutes > 0 && options.RefreshTokenDays > 0,
                "Jwt:AccessTokenMinutes and Jwt:RefreshTokenDays must be greater than zero.")
            .ValidateOnStart();

        services
            .AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        services.AddMemoryCache();

        // Identity "core": UserManager, RoleManager, PasswordHasher (PBKDF2), validators, lockout.
        // No cookies and no SignInManager: this is a pure JWT API.
        services
            .AddIdentityCore<ApplicationUser>(ConfigureIdentityOptions)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<ArabicIdentityErrorDescriber>();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IUserSessionCache, UserSessionCache>();
        services.AddScoped<AuditLogWriter>();
        services.AddScoped<UserAccessReader>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }

    /// <summary>
    /// Password rules here MUST match Application/Validators/Common/ValidationRules.cs.
    /// </summary>
    private static void ConfigureIdentityOptions(IdentityOptions options)
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@";

        // E-mail confirmation is not used: administrators create the accounts.
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    }
}
