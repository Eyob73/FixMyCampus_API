using System.Security.Claims;
using FixMyCampus.Application.DTOs.Tickets;
using FixMyCampus.Application.Exceptions;
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
    public async Task<IActionResult> CreateTicket()
    {
        try
        {
            CreateTicketRequest request;
            List<IFormFile> files = new();

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                request = new CreateTicketRequest
                {
                    Title = form["title"].ToString(),
                    Category = form["category"].ToString(),
                    Building = form["building"].ToString(),
                    Floor = form.ContainsKey("floor") ? form["floor"].ToString() : null,
                    Room = form.ContainsKey("room") ? form["room"].ToString() : null,
                    Description = form["description"].ToString(),
                    Priority = Enum.TryParse<FixMyCampus.Domain.Enums.TicketPriority>(form["priority"].ToString(), true, out var p) ? p : FixMyCampus.Domain.Enums.TicketPriority.Medium,
                    IgnorePotentialDuplicates = form.ContainsKey("ignorePotentialDuplicates") && bool.TryParse(form["ignorePotentialDuplicates"], out var ign) && ign
                };
                files = form.Files.ToList();
            }
            else
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                request = System.Text.Json.JsonSerializer.Deserialize<CreateTicketRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new ArgumentException("Invalid ticket request payload.");
            }

            var ticket = await _ticketService.CreateTicketAsync(CurrentUserId, request, files);
            return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
        }
        catch (DuplicateTicketException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new DuplicateConflictResponse
            {
                Message = ex.Message,
                DuplicateTicket = ex.DuplicateTicket,
                Matches = ex.Matches.ToList()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("check-duplicate")]
    [Authorize(Roles = "Reporter")]
    public async Task<IActionResult> CheckDuplicate([FromBody] DuplicateCheckRequest request)
    {
        var result = await _ticketService.CheckDuplicateTicketAsync(request);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] TicketFilterRequest filter)
    {
        if (User.IsInRole("Reporter") && !User.IsInRole("Admin") && !User.IsInRole("Technician"))
        {
            var myTickets = await _ticketService.GetMyTicketsAsync(CurrentUserId, filter);
            return Ok(myTickets);
        }

        var tickets = await _ticketService.GetTicketsAsync(filter);
        return Ok(tickets);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Reporter")]
    public async Task<IActionResult> GetMyTickets([FromQuery] TicketFilterRequest? filter)
    {
        var tickets = await _ticketService.GetMyTicketsAsync(CurrentUserId, filter);
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request)
    {
        try
        {
            var comment = await _ticketService.AddCommentAsync(id, CurrentUserId, IsAdmin, request);
            return Ok(comment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("{id}/internal-notes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddInternalNote(Guid id, [FromBody] AddNoteRequest request)
    {
        try
        {
            var ticket = await _ticketService.AddInternalNoteAsync(id, CurrentUserId, IsAdmin, request);
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPatch("{id}/priority")]
    public async Task<IActionResult> UpdatePriority(Guid id, [FromBody] UpdatePriorityRequest request)
    {
        try
        {
            var ticket = await _ticketService.UpdatePriorityAsync(id, CurrentUserId, IsAdmin, request.Priority);
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPut("{id}/status")]
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var status = request.ParsedStatus;
            var ticket = await _ticketService.UpdateStatusAsync(id, CurrentUserId, IsAdmin, status, request.Note);
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
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "Reporter")]
    public async Task<IActionResult> GetReporterDashboard()
    {
        try
        {
            var stats = await _ticketService.GetReporterDashboardAsync(CurrentUserId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("{ticketId}/images/{imageId}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTicketImageFile(Guid ticketId, Guid imageId)
    {
        try
        {
            var (stream, contentType, fileName) = await _ticketService.GetTicketImageStreamAsync(ticketId, imageId);
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Image or ticket not found" });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { Message = "Image file not found on storage" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("{id}/images")]
    public async Task<IActionResult> UploadImages(Guid id, [FromForm] List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                if (Request.HasFormContentType && Request.Form.Files.Count > 0)
                {
                    files = Request.Form.Files.ToList();
                }
                else
                {
                    return BadRequest(new { Message = "No image files provided." });
                }
            }

            var category = FixMyCampus.Domain.Enums.ImageCategory.ReportedIssue;
            var images = await _ticketService.AddTicketImagesAsync(id, CurrentUserId, IsAdmin, category, files);
            return Ok(images);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Message = "Ticket not found" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTicket(Guid id)
    {
        try
        {
            await _ticketService.DeleteTicketAsync(id, CurrentUserId);
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
