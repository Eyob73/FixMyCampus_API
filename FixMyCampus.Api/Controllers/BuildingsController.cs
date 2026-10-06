using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixMyCampus.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class BuildingsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetBuildings()
    {
        var buildings = new[]
        {
            new {
                Id = "bld-01",
                Code = "SCI-A",
                Name = "Physical Sciences Hall & Labs",
                Zone = "STEM_COMPLEX",
                Floors = 5,
                TotalRooms = 120,
                ActiveTicketsCount = 7,
                ManagerName = "Robert Vance",
                ManagerContact = "r.vance@facilities.campus.edu",
                Status = "OPERATIONAL",
                CreatedAt = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new {
                Id = "bld-02",
                Code = "ENG-MAIN",
                Name = "Engineering Research Center",
                Zone = "STEM_COMPLEX",
                Floors = 6,
                TotalRooms = 145,
                ActiveTicketsCount = 4,
                ManagerName = "Kirsten Dale",
                ManagerContact = "k.dale@facilities.campus.edu",
                Status = "OPERATIONAL",
                CreatedAt = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc)
            },
            new {
                Id = "bld-03",
                Code = "LIB-CENTRAL",
                Name = "W.E.B. Du Bois Memorial Library",
                Zone = "CENTRAL_QUAD",
                Floors = 8,
                TotalRooms = 90,
                ActiveTicketsCount = 2,
                ManagerName = "Arthur Dent",
                ManagerContact = "a.dent@facilities.campus.edu",
                Status = "OPERATIONAL",
                CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new {
                Id = "bld-04",
                Code = "RES-NORTH",
                Name = "North Quad Residential Commons",
                Zone = "NORTH_CAMPUS",
                Floors = 4,
                TotalRooms = 210,
                ActiveTicketsCount = 8,
                ManagerName = "Maria Gallagher",
                ManagerContact = "m.gallagher@facilities.campus.edu",
                Status = "MAINTENANCE_SURGE",
                CreatedAt = new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new {
                Id = "bld-05",
                Code = "ATH-PAV",
                Name = "Campus Pavilion & Aquatic Center",
                Zone = "WEST_ATHLETICS",
                Floors = 3,
                TotalRooms = 45,
                ActiveTicketsCount = 3,
                ManagerName = "Coach Thomas Harris",
                ManagerContact = "t.harris@athletics.campus.edu",
                Status = "OPERATIONAL",
                CreatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new {
                Id = "bld-06",
                Code = "ADM-TOWER",
                Name = "Founders Administrative Tower",
                Zone = "CENTRAL_QUAD",
                Floors = 10,
                TotalRooms = 160,
                ActiveTicketsCount = 1,
                ManagerName = "Helen Morales",
                ManagerContact = "h.morales@facilities.campus.edu",
                Status = "OPERATIONAL",
                CreatedAt = new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        return Ok(buildings);
    }

    [HttpPost]
    public IActionResult CreateBuilding([FromBody] dynamic body)
    {
        return Ok(body);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateBuilding(string id, [FromBody] dynamic body)
    {
        return Ok(body);
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteBuilding(string id)
    {
        return Ok();
    }
}
