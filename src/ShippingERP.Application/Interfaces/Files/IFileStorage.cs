using ShippingERP.Application.Common.Files;

namespace ShippingERP.Application.Interfaces.Files;

/// <summary>
/// Stores uploaded files outside the web root under generated names.
///
/// SaveAsync rejects (400 VALIDATION_ERROR) files that are empty, too large, or whose CONTENT is not an
/// allowed type (JPEG, PNG, WEBP, PDF): the extension and content type sent by the client are not trusted.
/// Only the storage key is saved in the database; physical paths are never returned to the client.
/// </summary>
public interface IFileStorage
{
    /// <param name="file">The uploaded file.</param>
    /// <param name="folder">Logical folder, e.g. "shipments". A year/month sub-folder is added automatically.</param>
    /// <param name="fieldName">Form field name used in the validation error (e.g. "signature").</param>
    Task<StoredFile> SaveAsync(UploadedFile file, string folder, string fieldName, CancellationToken cancellationToken);

    /// <summary>Opens a stored file. Throws NotFoundException when it does not exist.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Removes a stored file (used to clean up when the database transaction fails).</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

/// <param name="StorageKey">Relative key, e.g. "shipments/2026/10/9f2c...e1.jpg".</param>
/// <param name="ContentType">Detected from the content, e.g. "image/jpeg".</param>
/// <param name="SizeBytes">Size of the stored file.</param>
/// <param name="Sha256">Hex SHA-256 of the content.</param>
/// <param name="SafeFileName">The client's file name with unsafe characters removed (display only).</param>
public sealed record StoredFile(string StorageKey, string ContentType, long SizeBytes, string Sha256, string SafeFileName);
