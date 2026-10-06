using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _adminService.GetDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets([FromQuery] TicketFilterRequest filter)
    {
        var tickets = await _adminService.GetTicketsAsync(filter);
        return Ok(tickets);
    }

    [HttpGet("technicians")]
    public async Task<IActionResult> GetTechnicians()
    {
        var technicians = await _adminService.GetTechniciansAsync();
        return Ok(technicians);
    }

    [HttpGet("reporters")]
    public async Task<IActionResult> GetReporters()
    {
        var reporters = await _adminService.GetReportersAsync();
        return Ok(reporters);
    }

    [HttpPatch("reporters/{id}/status")]
    public async Task<IActionResult> ToggleReporterStatus(Guid id, [FromBody] dynamic body)
    {
        // Mocked to prevent breaking frontend for now
        var reporters = await _adminService.GetReportersAsync();
        var reporter = reporters.FirstOrDefault(r => r.Id == id);
        if (reporter == null) return NotFound();
        return Ok(reporter);
    }

    [HttpPut("tickets/{id}/assign")]
    public async Task<IActionResult> AssignTechnician(Guid id, [FromBody] AssignTechnicianRequest request)
    {
        try
        {
            var ticket = await _adminService.AssignTechnicianAsync(id, request.TechnicianId, CurrentUserId);
            return Ok(ticket);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
