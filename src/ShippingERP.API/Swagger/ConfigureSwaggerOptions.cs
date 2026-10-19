using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ShippingERP.API.Swagger;

/// <summary>
/// Creates one Swagger document per API version (v1, v2, ...).
/// When we add /api/v2/ later, a new "V2" entry appears in Swagger UI automatically.
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfo(description));
        }
    }

    private static OpenApiInfo CreateInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Shipping ERP API - نظام إدارة الشحن والخدمات اللوجستية",
            Version = description.ApiVersion.ToString(),
            Description = "واجهة برمجة التطبيقات لنظام إدارة شركة الشحن السريع. " +
                          "جميع التواريخ بتوقيت UTC وجميع المبالغ من نوع decimal."
        };

        if (description.IsDeprecated)
        {
            info.Description += " (هذا الإصدار متوقف وسيتم إزالته لاحقاً)";
        }

        return info;
    }
}
