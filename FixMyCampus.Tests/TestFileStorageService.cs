using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FixMyCampus.Tests;

public class TestFileStorageService : IFileStorageService
{
    public List<string> SavedFiles { get; } = new();
    public List<string> DeletedFiles { get; } = new();

    public Task<string> SaveFileAsync(IFormFile file, string subDirectory, CancellationToken cancellationToken = default)
    {
        var path = $"{subDirectory}/{Guid.NewGuid()}_{file.FileName}";
        SavedFiles.Add(path);
        return Task.FromResult(path);
    }

    public Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(
        string relativePath,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        return Task.FromResult<(Stream Stream, string ContentType, string FileName)>((stream, contentType, originalFileName));
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        DeletedFiles.Add(relativePath);
        SavedFiles.Remove(relativePath);
        return Task.CompletedTask;
    }

    public Task DeleteFilesAsync(IEnumerable<string> relativePaths, CancellationToken cancellationToken = default)
    {
        foreach (var p in relativePaths)
        {
            DeletedFiles.Add(p);
            SavedFiles.Remove(p);
        }
        return Task.CompletedTask;
    }
}
