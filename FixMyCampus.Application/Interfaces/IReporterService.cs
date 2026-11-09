using FixMyCampus.Application.DTOs.Users;

namespace FixMyCampus.Application.Interfaces;

public interface IReporterService
{
    Task<PaginatedResult<ReporterDto>> GetReportersAsync(ReporterQueryParameters parameters);
    Task<IReadOnlyList<ReporterDto>> GetAllReportersAsync();
    Task<ReporterStatsDto> GetStatsAsync();
    Task<ReporterDetailDto> GetReporterByIdAsync(Guid id);
    Task<ReporterDto> CreateReporterAsync(CreateReporterDto request);
    Task<ReporterDto> UpdateReporterAsync(Guid id, UpdateReporterDto request);
    Task DeleteReporterAsync(Guid id);
    Task<ReporterDto> UpdateStatusAsync(Guid id, string status);
}

