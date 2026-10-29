using FixMyCampus.Application.DTOs.Tickets;

namespace FixMyCampus.Application.Exceptions;

public class DuplicateTicketException : Exception
{
    public DuplicateTicketMatchDto DuplicateTicket { get; }
    public IReadOnlyList<DuplicateTicketMatchDto> Matches { get; }
    public bool IsConfirmed { get; }

    public DuplicateTicketException(
        string message,
        DuplicateTicketMatchDto duplicateTicket,
        IEnumerable<DuplicateTicketMatchDto>? matches = null,
        bool isConfirmed = true)
        : base(message)
    {
        DuplicateTicket = duplicateTicket;
        Matches = matches?.ToList() ?? new List<DuplicateTicketMatchDto> { duplicateTicket };
        IsConfirmed = isConfirmed;
    }
}
