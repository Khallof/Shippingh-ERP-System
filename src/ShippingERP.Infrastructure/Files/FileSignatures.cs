namespace ShippingERP.Infrastructure.Files;

/// <summary>
/// Detects the real file type from the first bytes ("magic numbers"). The client's file name and content type
/// are not trusted: a script renamed to photo.jpg is rejected because it does not start with the JPEG signature.
/// Only these types are accepted: JPEG, PNG, WEBP and PDF.
/// </summary>
public static class FileSignatures
{
    public sealed record FileKind(string ContentType, string Extension);

    public static readonly FileKind Jpeg = new("image/jpeg", ".jpg");
    public static readonly FileKind Png = new("image/png", ".png");
    public static readonly FileKind Webp = new("image/webp", ".webp");
    public static readonly FileKind Pdf = new("application/pdf", ".pdf");

    /// <summary>Extensions the client may use for each type.</summary>
    private static readonly Dictionary<FileKind, string[]> AllowedExtensions = new()
    {
        [Jpeg] = new[] { ".jpg", ".jpeg" },
        [Png] = new[] { ".png" },
        [Webp] = new[] { ".webp" },
        [Pdf] = new[] { ".pdf" }
    };

    /// <summary>Number of bytes needed by <see cref="Detect"/>.</summary>
    public const int HeaderLength = 12;

    public static FileKind? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return Jpeg;
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return Png;
        }

        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            return Webp;
        }

        if (header.Length >= 5 &&
            header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F' && header[4] == (byte)'-')
        {
            return Pdf;
        }

        return null;
    }

    /// <summary>True when the client's extension matches the detected type (photo.png containing a JPEG is refused).</summary>
    public static bool ExtensionMatches(FileKind kind, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.TryGetValue(kind, out var allowed) && allowed.Contains(extension);
    }

    /// <summary>
    /// Excel (.xlsx) files are ZIP archives ("PK\x03\x04"). Used by the import; a real check of the content
    /// is done by the Excel reader afterwards.
    /// </summary>
    public static bool IsZip(ReadOnlySpan<byte> header) =>
        header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
}
