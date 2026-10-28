using FixMyCampus.Application.DTOs.Categories;
using FixMyCampus.Domain.Entities;
using FixMyCampus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FixMyCampus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Icon = c.Icon,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategoryById(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound(new { message = $"Category with ID '{id}' was not found." });
        }

        var dto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == dto.Name.Trim().ToLower());
        if (exists)
        {
            return BadRequest(new { message = $"A category named '{dto.Name}' already exists." });
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Icon = dto.Icon?.Trim(),
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var responseDto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

        return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, responseDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound(new { message = $"Category with ID '{id}' was not found." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Trim().ToLower() != category.Name.ToLower())
        {
            var exists = await _context.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == dto.Name.Trim().ToLower());
            if (exists)
            {
                return BadRequest(new { message = $"A category named '{dto.Name}' already exists." });
            }
            category.Name = dto.Name.Trim();
        }

        if (dto.Description != null) category.Description = dto.Description.Trim();
        if (dto.Icon != null) category.Icon = dto.Icon.Trim();
        if (dto.DisplayOrder.HasValue) category.DisplayOrder = dto.DisplayOrder.Value;
        if (dto.IsActive.HasValue) category.IsActive = dto.IsActive.Value;

        category.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var responseDto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Icon = category.Icon,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

        return Ok(responseDto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound(new { message = $"Category with ID '{id}' was not found." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
