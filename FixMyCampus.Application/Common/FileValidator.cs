using Microsoft.AspNetCore.Http;

namespace FixMyCampus.Application.Common;

public static class FileValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxImageCount = 5;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public static void ValidateImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Uploaded file cannot be empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File '{file.FileName}' exceeds the maximum allowed size of 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException($"File '{file.FileName}' has an unsupported extension. Allowed formats: JPEG, PNG, WebP.");
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"File '{file.FileName}' has an unsupported content type: '{file.ContentType}'.");
        }

        // Validate magic bytes (file signature)
        ValidateMagicBytes(file);
    }

    private static void ValidateMagicBytes(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        if (stream.Length < 12)
        {
            throw new ArgumentException($"File '{file.FileName}' is too small to be a valid image.");
        }

        var header = new byte[12];
        int bytesRead = stream.Read(header, 0, header.Length);
        if (bytesRead < 12)
        {
            throw new ArgumentException($"Unable to verify header for file '{file.FileName}'.");
        }

        // JPEG: FF D8 FF
        bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                     header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

        // WebP: RIFF (52 49 46 46) .... WEBP (57 45 42 50)
        bool isWebp = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                      header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

        if (!isJpeg && !isPng && !isWebp)
        {
            throw new ArgumentException($"File '{file.FileName}' does not match a valid JPEG, PNG, or WebP image signature.");
        }
    }
}
