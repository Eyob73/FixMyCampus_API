using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class MetadataController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MetadataController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("buildings")]
    public async Task<IActionResult> GetBuildings()
    {
        var buildings = await _context.Buildings
            .OrderBy(b => b.Name)
            .Select(b => b.Name)
            .ToListAsync();

        return Ok(buildings);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }
}
