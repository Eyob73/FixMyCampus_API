using FixMyCampus.Domain.Enums;
using System.Text.Json;

namespace FixMyCampus.Application.DTOs.Tickets;

public class UpdateStatusRequest
{
    public object? Status { get; set; }
    public string? Note { get; set; }

    public TicketStatus ParsedStatus => ParseStatus(Status);

    public static TicketStatus ParseStatus(object? raw)
    {
        if (raw == null) return TicketStatus.New;

        if (raw is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var intVal))
            {
                return Enum.IsDefined(typeof(TicketStatus), intVal) ? (TicketStatus)intVal : TicketStatus.New;
            }
            if (elem.ValueKind == JsonValueKind.String)
            {
                var str = elem.GetString()?.Replace("_", "").Trim() ?? "";
                if (Enum.TryParse<TicketStatus>(str, true, out var parsed))
                {
                    return parsed;
                }
            }
        }
        else if (raw is int i)
        {
            return Enum.IsDefined(typeof(TicketStatus), i) ? (TicketStatus)i : TicketStatus.New;
        }
        else if (raw is long l)
        {
            return Enum.IsDefined(typeof(TicketStatus), (int)l) ? (TicketStatus)(int)l : TicketStatus.New;
        }
        else if (raw is string s)
        {
            var str = s.Replace("_", "").Trim();
            if (Enum.TryParse<TicketStatus>(str, true, out var parsed))
            {
                return parsed;
            }
        }
        else if (raw is TicketStatus ts)
        {
            return ts;
        }

        return TicketStatus.New;
    }
}
