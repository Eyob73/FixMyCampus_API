using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class MetadataController : ControllerBase
{
    private static readonly string[] CampusBuildings = new[]
    {
        "Science Building",
        "Student Union",
        "North Residential Hall",
        "South Residential Hall",
        "University Library",
        "Engineering Annex",
        "Health Sciences Center",
        "Athletics Complex",
        "Humanities Hall",
        "Dining Hall & Campus Center"
    };

    private static readonly string[] TicketCategories = new[]
    {
        "Plumbing",
        "Electrical",
        "HVAC / Climate",
        "Equipment",
        "Furniture",
        "Structural & Doors",
        "Network & Wi-Fi",
        "Cleaning & Grounds",
        "Safety & Locks",
        "Other"
    };

    [HttpGet("buildings")]
    public IActionResult GetBuildings()
    {
        return Ok(CampusBuildings);
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        return Ok(TicketCategories);
    }
}
