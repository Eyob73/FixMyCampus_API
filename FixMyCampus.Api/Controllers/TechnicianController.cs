using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize(Roles = "Technician")]
[ApiController]
[Route("api/[controller]")]
public class TechnicianController : ControllerBase
{
    private readonly ITechnicianService _technicianService;

    public TechnicianController(ITechnicianService technicianService)
    {
        _technicianService = technicianService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _technicianService.GetDashboardAsync(CurrentUserId);
        return Ok(dashboard);
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets([FromQuery] TicketFilterRequest filter)
    {
        var tickets = await _technicianService.GetAssignedTicketsAsync(CurrentUserId, filter);
        return Ok(tickets);
    }

    [HttpPut("tickets/{id}/start")]
    public async Task<IActionResult> StartWork(Guid id)
    {
        try
        {
            var ticket = await _technicianService.StartWorkAsync(id, CurrentUserId);
            return Ok(ticket);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
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

    [HttpPut("tickets/{id}/resolve")]
    public async Task<IActionResult> ResolveTicket(Guid id)
    {
        try
        {
            var ticket = await _technicianService.ResolveTicketAsync(id, CurrentUserId);
            return Ok(ticket);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
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
