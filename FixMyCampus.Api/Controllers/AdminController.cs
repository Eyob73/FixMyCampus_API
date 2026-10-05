using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

// [Authorize(Roles = "Admin")]
[AllowAnonymous]
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
