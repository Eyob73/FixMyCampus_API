using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.DTOs.Users;
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
    private readonly ITicketService _ticketService;
    private readonly IReporterService _reporterService;

    public AdminController(
        IAdminService adminService,
        ITicketService ticketService,
        IReporterService reporterService)
    {
        _adminService = adminService;
        _ticketService = ticketService;
        _reporterService = reporterService;
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
    public async Task<IActionResult> GetTechnicians([FromQuery] TechnicianQueryParameters? parameters, [FromQuery] bool all = false)
    {
        if (all)
        {
            var technicians = await _adminService.GetTechniciansAsync();
            return Ok(technicians);
        }

        var result = await _adminService.GetTechniciansAsync(parameters ?? new TechnicianQueryParameters());
        return Ok(result);
    }

    [HttpGet("technicians/stats")]
    public async Task<IActionResult> GetTechnicianStats()
    {
        var stats = await _adminService.GetTechnicianStatsAsync();
        return Ok(stats);
    }

    [HttpGet("technicians/{id}")]
    public async Task<IActionResult> GetTechnicianById(Guid id)
    {
        try
        {
            var technician = await _adminService.GetTechnicianByIdAsync(id);
            return Ok(technician);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpPost("technicians")]
    public async Task<IActionResult> CreateTechnician([FromBody] FixMyCampus.Application.DTOs.Users.CreateTechnicianDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var technician = await _adminService.CreateTechnicianAsync(request);
            return CreatedAtAction(nameof(GetTechnicianById), new { id = technician.Id }, technician);
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

    [HttpPut("technicians/{id}")]
    public async Task<IActionResult> UpdateTechnician(Guid id, [FromBody] FixMyCampus.Application.DTOs.Users.UpdateTechnicianDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var technician = await _adminService.UpdateTechnicianAsync(id, request);
            return Ok(technician);
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

    [HttpDelete("technicians/{id}")]
    public async Task<IActionResult> DeleteTechnician(Guid id)
    {
        try
        {
            await _adminService.DeleteTechnicianAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPatch("technicians/{id}/status")]
    public async Task<IActionResult> UpdateTechnicianStatus(Guid id, [FromBody] FixMyCampus.Application.DTOs.Users.UpdateTechnicianStatusDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var technician = await _adminService.UpdateTechnicianStatusAsync(id, request.Status);
            return Ok(technician);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("reporters")]
    public async Task<IActionResult> GetReporters([FromQuery] ReporterQueryParameters parameters, [FromQuery] bool all = false)
    {
        if (all)
        {
            var allReporters = await _reporterService.GetAllReportersAsync();
            return Ok(allReporters);
        }

        var result = await _reporterService.GetReportersAsync(parameters);
        return Ok(result);
    }

    [HttpGet("reporters/stats")]
    public async Task<IActionResult> GetReporterStats()
    {
        var stats = await _reporterService.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("reporters/{id}")]
    public async Task<IActionResult> GetReporterById(Guid id)
    {
        try
        {
            var reporter = await _reporterService.GetReporterByIdAsync(id);
            return Ok(reporter);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpPost("reporters")]
    public async Task<IActionResult> CreateReporter([FromBody] CreateReporterDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var reporter = await _reporterService.CreateReporterAsync(request);
            return CreatedAtAction(nameof(GetReporterById), new { id = reporter.Id }, reporter);
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

    [HttpPut("reporters/{id}")]
    public async Task<IActionResult> UpdateReporter(Guid id, [FromBody] UpdateReporterDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var reporter = await _reporterService.UpdateReporterAsync(id, request);
            return Ok(reporter);
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

    [HttpDelete("reporters/{id}")]
    public async Task<IActionResult> DeleteReporter(Guid id)
    {
        try
        {
            await _reporterService.DeleteReporterAsync(id);
            return NoContent();
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

    [HttpPatch("reporters/{id}/status")]
    public async Task<IActionResult> UpdateReporterStatus(Guid id, [FromBody] UpdateReporterStatusDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var reporter = await _reporterService.UpdateStatusAsync(id, request.Status);
            return Ok(reporter);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
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

    [HttpPut("tickets/{id}/status")]
    [HttpPatch("tickets/{id}/status")]
    public async Task<IActionResult> UpdateTicketStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var status = request.ParsedStatus;
            var ticket = await _ticketService.UpdateStatusAsync(id, CurrentUserId, true, status, request.Note);
            return Ok(ticket);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpDelete("tickets/{id}")]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        try
        {
            await _adminService.DeleteTicketAsync(id, CurrentUserId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
