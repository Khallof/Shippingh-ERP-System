using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Files;

namespace ShippingERP.Infrastructure.Files;

/// <summary>
/// Stores files on the local disk (or a mounted volume) under generated names:
///     {root}/{folder}/{yyyy}/{MM}/{random-guid}{.ext}
///
/// Security:
/// - the client's file name is never used in the path (no path traversal, no overwriting);
/// - the type is detected from the content and must match the extension (JPEG, PNG, WEBP, PDF only);
/// - the size limit is enforced while copying (a fake Content-Length cannot bypass it);
/// - every resolved path is checked to stay inside the root folder;
/// - only the relative storage key is saved in the database and nothing about the disk is returned to clients.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 81920;

    private readonly FileStorageOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(_options.RootPath) ? "App_Data/uploads" : _options.RootPath);
    }

    public async Task<StoredFile> SaveAsync(UploadedFile file, string folder, string fieldName, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw Invalid(fieldName, "الملف فارغ.");
        }

        if (file.Length > _options.MaxFileSizeBytes)
        {
            throw Invalid(fieldName, $"حجم الملف أكبر من الحد المسموح ({_options.MaxFileSizeBytes / (1024 * 1024)} ميغابايت).");
        }

        await using var input = file.OpenReadStream();

        var header = new byte[FileSignatures.HeaderLength];
        var headerLength = await ReadAtLeastAsync(input, header, cancellationToken);
        var kind = FileSignatures.Detect(header.AsSpan(0, headerLength));

        if (kind is null)
        {
            throw Invalid(fieldName, "نوع الملف غير مسموح. الأنواع المسموحة: JPG, PNG, WEBP, PDF.");
        }

        if (!FileSignatures.ExtensionMatches(kind, file.FileName))
        {
            throw Invalid(fieldName, $"امتداد الملف لا يطابق محتواه (المحتوى من نوع {kind.Extension}).");
        }

        var now = _dateTimeProvider.UtcNow;
        var storageKey = string.Join('/',
            SanitizeFolder(folder),
            now.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
            now.Month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            Guid.NewGuid().ToString("N") + kind.Extension);

        var fullPath = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long size = 0;
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        try
        {
            await using (var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                await output.WriteAsync(header.AsMemory(0, headerLength), cancellationToken);
                sha.AppendData(header, 0, headerLength);
                size += headerLength;

                var buffer = new byte[BufferSize];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > _options.MaxFileSizeBytes)
                    {
                        throw Invalid(fieldName, $"حجم الملف أكبر من الحد المسموح ({_options.MaxFileSizeBytes / (1024 * 1024)} ميغابايت).");
                    }

                    sha.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
        }
        catch
        {
            TryDelete(fullPath);
            throw;
        }

        var hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
        return new StoredFile(storageKey, kind.ContentType, size, hash, SafeFileName(file.FileName, kind.Extension));
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath))
        {
            throw new NotFoundException("الملف غير موجود في مساحة التخزين.");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        TryDelete(ResolvePath(storageKey));
        return Task.CompletedTask;
    }

    /// <summary>Display name: no folders, no control or path characters, at most 100 characters.</summary>
    public static string SafeFileName(string? fileName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName ?? string.Empty));
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            if (!char.IsControl(character) && !invalid.Contains(character) && character is not ('/' or '\\' or ':' or '"' or '<' or '>' or '|' or '*' or '?'))
            {
                builder.Append(character);
            }
        }

        var safe = builder.ToString().Trim().Trim('.');
        if (safe.Length == 0)
        {
            safe = "file";
        }

        if (safe.Length > 100)
        {
            safe = safe[..100];
        }

        return safe + extension;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storageKey))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar) ? _rootPath : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage key resolves outside the storage root.");
        }

        return fullPath;
    }

    private static string SanitizeFolder(string folder)
    {
        var safe = new string((folder ?? string.Empty).ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-').ToArray());
        return safe.Length == 0 ? "files" : safe;
    }

    private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private static void TryDelete(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Best effort: an orphan file is harmless (it is never referenced).
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });
}
