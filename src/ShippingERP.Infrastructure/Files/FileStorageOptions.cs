namespace ShippingERP.Infrastructure.Files;

/// <summary>
/// "FileStorage" section of appsettings.json.
/// RootPath: folder for uploaded files, OUTSIDE wwwroot (never served directly). A relative path is resolved
/// from the application's working directory; use an absolute path (or a Docker volume) in production.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "App_Data/uploads";

    /// <summary>Maximum size of one uploaded file (default 5 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}
