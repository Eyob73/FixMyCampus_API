using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixMyCampus.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var configuredPath = configuration["FileStorage:BasePath"];
        _storageRoot = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.Combine(environment.ContentRootPath, "App_Data", "uploads");

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subDirectory, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Cannot save an empty file.");
        }

        // Clean subDirectory and generate secure GUID file name
        var safeSubDir = subDirectory.Replace("..", "").Trim('/', '\\');
        var targetDir = Path.Combine(_storageRoot, safeSubDir);
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var secureFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDir, secureFileName);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        var relativePath = Path.Combine(safeSubDir, secureFileName).Replace('\\', '/');
        _logger.LogInformation("Saved file {OriginalName} as {RelativePath}", file.FileName, relativePath);
        return relativePath;
    }

    public Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(
        string relativePath,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var sanitizedRelative = relativePath.Replace("..", "").TrimStart('/', '\\');
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, sanitizedRelative));

        // Ensure no path traversal outside _storageRoot
        if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Invalid file path detected.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Requested image file not found on disk.");
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(((Stream)stream, contentType, originalFileName));
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.CompletedTask;

        try
        {
            var sanitizedRelative = relativePath.Replace("..", "").TrimStart('/', '\\');
            var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, sanitizedRelative));

            if (fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file {RelativePath}", relativePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {RelativePath}", relativePath);
        }

        return Task.CompletedTask;
    }

    public async Task DeleteFilesAsync(IEnumerable<string> relativePaths, CancellationToken cancellationToken = default)
    {
        foreach (var path in relativePaths)
        {
            await DeleteFileAsync(path, cancellationToken);
        }
    }
}
