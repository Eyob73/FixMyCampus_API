using Microsoft.AspNetCore.Http;

namespace FixMyCampus.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string subDirectory, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(string relativePath, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default);
    Task DeleteFilesAsync(IEnumerable<string> relativePaths, CancellationToken cancellationToken = default);
}
