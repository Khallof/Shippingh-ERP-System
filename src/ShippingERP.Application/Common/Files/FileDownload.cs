namespace ShippingERP.Application.Common.Files;

/// <summary>A file returned to the client (label, Excel template, attachment). The controller disposes the stream.</summary>
public sealed class FileDownload
{
    public FileDownload(Stream content, string contentType, string fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    /// <summary>Suggested download name (safe characters only).</summary>
    public string FileName { get; }

    public static FileDownload FromBytes(byte[] bytes, string contentType, string fileName) =>
        new(new MemoryStream(bytes, writable: false), contentType, fileName);
}
