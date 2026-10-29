using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.Interfaces;

public interface IDuplicateDetectionService
{
    Task<DuplicateCheckResponse> CheckDuplicatesAsync(
        DuplicateCheckRequest request,
        CancellationToken cancellationToken = default);

    Task<DuplicateCheckResponse> CheckDuplicatesAsync(
        string category,
        string building,
        string? floor,
        string? room,
        string title,
        string description,
        CancellationToken cancellationToken = default);
}
