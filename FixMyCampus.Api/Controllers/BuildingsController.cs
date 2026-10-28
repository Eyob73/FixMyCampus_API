using FixMyCampus.Application.DTOs.Buildings;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Domain.Enums;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class BuildingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BuildingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetBuildings()
    {
        var buildings = await _context.Buildings
            .OrderBy(b => b.Name)
            .ToListAsync();

        var activeTicketCounts = await _context.Tickets
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .GroupBy(t => t.Building)
            .Select(g => new { BuildingName = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BuildingName, x => x.Count);

        var result = buildings.Select(b => new BuildingDto
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Zone = b.Zone,
            Floors = b.Floors,
            TotalRooms = b.TotalRooms,
            ActiveTicketsCount = activeTicketCounts.TryGetValue(b.Name, out var count) ? count : 0,
            ManagerName = b.ManagerName,
            ManagerContact = b.ManagerContact,
            Status = b.Status,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBuildingById(Guid id)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return NotFound(new { message = $"Building with ID '{id}' was not found." });
        }

        var activeTicketsCount = await _context.Tickets
            .CountAsync(t => t.Building == building.Name && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

        var dto = new BuildingDto
        {
            Id = building.Id,
            Code = building.Code,
            Name = building.Name,
            Zone = building.Zone,
            Floors = building.Floors,
            TotalRooms = building.TotalRooms,
            ActiveTicketsCount = activeTicketsCount,
            ManagerName = building.ManagerName,
            ManagerContact = building.ManagerContact,
            Status = building.Status,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBuilding([FromBody] CreateBuildingDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var codeExists = await _context.Buildings.AnyAsync(b => b.Code.ToUpper() == dto.Code.Trim().ToUpper());
        if (codeExists)
        {
            return BadRequest(new { message = $"A building with code '{dto.Code}' already exists." });
        }

        var building = new Building
        {
            Id = Guid.NewGuid(),
            Code = dto.Code.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Zone = dto.Zone.Trim(),
            Floors = dto.Floors,
            TotalRooms = dto.TotalRooms,
            ManagerName = string.IsNullOrWhiteSpace(dto.ManagerName) ? null : dto.ManagerName.Trim(),
            ManagerContact = string.IsNullOrWhiteSpace(dto.ManagerContact) ? null : dto.ManagerContact.Trim(),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "OPERATIONAL" : dto.Status.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Buildings.Add(building);
        await _context.SaveChangesAsync();

        var responseDto = new BuildingDto
        {
            Id = building.Id,
            Code = building.Code,
            Name = building.Name,
            Zone = building.Zone,
            Floors = building.Floors,
            TotalRooms = building.TotalRooms,
            ActiveTicketsCount = 0,
            ManagerName = building.ManagerName,
            ManagerContact = building.ManagerContact,
            Status = building.Status,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };

        return CreatedAtAction(nameof(GetBuildingById), new { id = building.Id }, responseDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBuilding(Guid id, [FromBody] UpdateBuildingDto dto)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return NotFound(new { message = $"Building with ID '{id}' was not found." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Code) && dto.Code.Trim().ToUpper() != building.Code)
        {
            var codeExists = await _context.Buildings.AnyAsync(b => b.Id != id && b.Code.ToUpper() == dto.Code.Trim().ToUpper());
            if (codeExists)
            {
                return BadRequest(new { message = $"A building with code '{dto.Code}' already exists." });
            }
            building.Code = dto.Code.Trim().ToUpper();
        }

        if (!string.IsNullOrWhiteSpace(dto.Name)) building.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Zone)) building.Zone = dto.Zone.Trim();
        if (dto.Floors.HasValue && dto.Floors.Value > 0) building.Floors = dto.Floors.Value;
        if (dto.TotalRooms.HasValue && dto.TotalRooms.Value > 0) building.TotalRooms = dto.TotalRooms.Value;
        if (dto.ManagerName != null) building.ManagerName = string.IsNullOrWhiteSpace(dto.ManagerName) ? null : dto.ManagerName.Trim();
        if (dto.ManagerContact != null) building.ManagerContact = string.IsNullOrWhiteSpace(dto.ManagerContact) ? null : dto.ManagerContact.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Status)) building.Status = dto.Status.Trim();

        building.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var activeTicketsCount = await _context.Tickets
            .CountAsync(t => t.Building == building.Name && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

        var responseDto = new BuildingDto
        {
            Id = building.Id,
            Code = building.Code,
            Name = building.Name,
            Zone = building.Zone,
            Floors = building.Floors,
            TotalRooms = building.TotalRooms,
            ActiveTicketsCount = activeTicketsCount,
            ManagerName = building.ManagerName,
            ManagerContact = building.ManagerContact,
            Status = building.Status,
            CreatedAt = building.CreatedAt,
            UpdatedAt = building.UpdatedAt
        };

        return Ok(responseDto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBuilding(Guid id)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return NotFound(new { message = $"Building with ID '{id}' was not found." });
        }

        _context.Buildings.Remove(building);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
