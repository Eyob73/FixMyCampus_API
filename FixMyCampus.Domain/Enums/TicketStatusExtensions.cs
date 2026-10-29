namespace FixMyCampus.Domain.Enums;

public static class TicketStatusExtensions
{
    public static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.New,
        TicketStatus.Assigned,
        TicketStatus.InProgress
    ];

    public static readonly TicketStatus[] ClosedStatuses =
    [
        TicketStatus.Resolved,
        TicketStatus.Closed
    ];

    public static bool IsActive(this TicketStatus status) =>
        status is TicketStatus.New or TicketStatus.Assigned or TicketStatus.InProgress;

    public static bool IsClosed(this TicketStatus status) =>
        status is TicketStatus.Resolved or TicketStatus.Closed;
}
