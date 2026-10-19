namespace ShippingERP.Application.Common.Files;

/// <summary>
/// A file received from the client, without depending on ASP.NET Core (IFormFile is converted to this in the API).
/// The declared name and content type come from the client and are NOT trusted: the file storage detects the
/// real type from the first bytes of the content.
/// </summary>
public sealed class UploadedFile
{
    public UploadedFile(string fileName, string? contentType, long length, Func<Stream> openReadStream)
    {
        FileName = fileName;
        ContentType = contentType;
        Length = length;
        OpenReadStream = openReadStream;
    }

    /// <summary>Name sent by the client (display only, never used as a path).</summary>
    public string FileName { get; }

    /// <summary>Content type sent by the client (ignored for security decisions).</summary>
    public string? ContentType { get; }

    public long Length { get; }

    /// <summary>Opens a new read stream over the content. The caller disposes it.</summary>
    public Func<Stream> OpenReadStream { get; }
}
