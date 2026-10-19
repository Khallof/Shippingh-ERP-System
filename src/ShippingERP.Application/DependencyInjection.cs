using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace ShippingERP.Application;

/// <summary>
/// Registers everything that belongs to the Application layer.
/// Called once from Program.cs: builder.Services.AddApplication();
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMapping(assembly);
        services.AddValidation(assembly);

        // Identity services (IAuthService, IUserService, IRoleService) are implemented in Infrastructure,
        // because they need ASP.NET Core Identity's UserManager and the DbContext.

        return services;
    }

    /// <summary>
    /// Mapster setup.
    /// - Each feature gets one class in the Mappings folder that implements Mapster's IRegister
    ///   (e.g. BranchMappingConfig). Scan() finds and applies them all.
    /// - Services receive MapsterMapper.IMapper through constructor injection:
    ///   var dto = _mapper.Map&lt;BranchDto&gt;(branch);
    /// - For EF Core queries use ProjectToType&lt;TDto&gt;(_mappingConfig) so SQL selects only the DTO columns.
    /// </summary>
    private static void AddMapping(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var config = new TypeAdapterConfig();

        // Fail fast: a DTO property that has no matching source property is an error, not a silent null.
        config.RequireExplicitMapping = false;
        config.RequireDestinationMemberSource = true;

        config.Scan(assembly);

        // Checks every registered mapping once at startup; a broken mapping stops the app immediately
        // instead of failing later in production.
        config.Compile();

        services.AddSingleton(config);
        services.AddScoped<IMapper>(serviceProvider =>
            new Mapper(serviceProvider.GetRequiredService<TypeAdapterConfig>()));
    }

    /// <summary>
    /// FluentValidation setup. Validators run automatically for every controller action argument
    /// (see the API's Filters/ValidationFilter.cs).
    /// </summary>
    private static void AddValidation(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        // Scans this assembly for AbstractValidator<T> classes (Validators folder).
        // Helper validators (UserDetailsRules<T>, PagedQueryRules<T>) are generic, so they are skipped by the scan
        // and only used through Include(...).
        services.AddValidatorsFromAssembly(assembly);

        // Report only the first failing rule per property (e.g. "required" instead of "required" + "too short").
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        // Default messages (for rules without WithMessage) in Arabic.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("ar");

        // Error keys in camelCase so they match the JSON field names the frontend sends ("userName", not "UserName").
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
    }
}
