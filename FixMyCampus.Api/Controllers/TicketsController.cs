using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost]
    [Authorize(Roles = "Reporter")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        try
        {
            var ticket = await _ticketService.CreateTicketAsync(CurrentUserId, request);
            return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] TicketFilterRequest filter)
    {
        var tickets = await _ticketService.GetTicketsAsync(filter);
        return Ok(tickets);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Reporter")]
    public async Task<IActionResult> GetMyTickets()
    {
        var tickets = await _ticketService.GetMyTicketsAsync(CurrentUserId);
        return Ok(tickets);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicketById(Guid id)
    {
        try
        {
            var ticket = await _ticketService.GetTicketByIdAsync(id, CurrentUserId, IsAdmin);
            return Ok(ticket);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetTicketHistory(Guid id)
    {
        try
        {
            var history = await _ticketService.GetTicketHistoryAsync(id, CurrentUserId, IsAdmin);
            return Ok(history);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
