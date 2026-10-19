using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using ShippingERP.API.BackgroundJobs;
using ShippingERP.API.Filters;
using ShippingERP.API.HealthChecks;
using ShippingERP.API.Security;
using ShippingERP.API.Services;
using ShippingERP.API.Swagger;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.Interfaces;

namespace ShippingERP.API.Extensions;

/// <summary>
/// Registers all API-layer services. Each concern has its own small method so a junior developer
/// can find "where is CORS configured?" quickly.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddControllersWithStandardResponses();
        services.AddApiVersioningConfiguration();
        services.AddSwaggerConfiguration();
        services.AddCorsConfiguration(configuration, environment);
        services.AddHealthCheckConfiguration();
        services.AddJwtAuthentication();
        services.AddPermissionAuthorization();
        services.AddRateLimitingConfiguration();
        services.AddBackgroundJobs(configuration);

        return services;
    }

    /// <summary>Hosted services. Step 5: the hourly invoice overdue check (Billing section of appsettings.json).</summary>
    private static void AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddHostedService<InvoiceOverdueBackgroundService>();

        // Step 7: automatic posting of the operational events (outbox -> journal entries). Settings: "Accounting".
        services.AddHostedService<AccountingPostingBackgroundService>();
    }

    private static void AddControllersWithStandardResponses(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                // Runs FluentValidation validators for every action argument (see Filters/ValidationFilter.cs).
                options.Filters.Add<ValidationFilter>();

                // FluentValidation is the single source of input rules (Arabic messages, camelCase field names).
                // Without this, MVC would also treat every non-nullable string as [Required] with English messages.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options =>
            {
                // Without this, Arabic text is escaped as ال... in JSON responses.
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Model-binding errors (bad JSON, wrong types, [Required] attributes) use the same format as everything else.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value is { Errors.Count: > 0 })
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors
                                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                    ? "القيمة المدخلة غير صالحة."
                                    : error.ErrorMessage)
                                .ToArray());

                    var response = ApiResponse<object>.Fail(
                        ErrorCodes.ValidationError,
                        RequestValidationException.DefaultMessage,
                        errors,
                        context.HttpContext.GetTraceId());

                    return new BadRequestObjectResult(response);
                };
            });
    }

    private static void AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true; // adds "api-supported-versions" header
                options.ApiVersionReader = new UrlSegmentApiVersionReader(); // version comes from /api/v1/...
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV"; // v1, v1.1, v2
                options.SubstituteApiVersionInUrl = true; // Swagger shows /api/v1/... instead of /api/v{version}/...
            });
    }

    private static void AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // The "Authorize" button: paste the accessToken from POST /api/v1/auth/login (without the word Bearer).
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "الصق قيمة accessToken الناتجة من POST /api/v1/auth/login (بدون كلمة Bearer)."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
        services.ConfigureOptions<ConfigureSwaggerOptions>();
    }

    private static void AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0 && environment.IsDevelopment())
        {
            // Common local frontend dev servers (React/Vite/Angular).
            allowedOrigins = new[]
            {
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:4200"
            };
        }

        services.AddCors(options =>
        {
            options.AddPolicy(ApiConstants.CorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .WithExposedHeaders(
                        "X-Trace-Id",
                        JwtAuthenticationSetup.TokenExpiredHeader,
                        "Retry-After",
                        "api-supported-versions",
                        "Content-Disposition");
            });
        });
    }

    private static void AddHealthCheckConfiguration(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(
                name: ApiConstants.DatabaseHealthCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { ApiConstants.ReadyHealthCheckTag, "db", "sql" });
    }

    /// <summary>Permission-based authorization: [HasPermission("Shipments.Create")].</summary>
    private static void AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
    }

    /// <summary>
    /// Limits sign-in, refresh, revoke and change-password calls per IP address (brute-force protection,
    /// in addition to Identity's account lockout). Defaults: 10 requests per 60 seconds.
    /// Configure with RateLimiting:AuthPermitLimit and RateLimiting:AuthWindowSeconds.
    /// </summary>
    private static void AddRateLimitingConfiguration(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ApiConstants.AuthRateLimitPolicy, httpContext =>
            {
                var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
                var windowSeconds = configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var response = ApiResponse<object>.Fail(
                    ErrorCodes.TooManyRequests,
                    "عدد كبير من المحاولات. يرجى الانتظار قليلاً ثم المحاولة مرة أخرى.",
                    null,
                    context.HttpContext.GetTraceId());

                await context.HttpContext.WriteApiResponseAsync(StatusCodes.Status429TooManyRequests, response);
            };
        });
    }
}
