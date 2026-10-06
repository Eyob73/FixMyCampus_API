using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetNotifications()
    {
        return Ok(new object[] { });
    }

    [HttpPatch("{id}/read")]
    public IActionResult MarkAsRead(string id)
    {
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public IActionResult MarkAllAsRead()
    {
        return Ok();
    }
}
