namespace HelpDesk.Api.Models;

/// <summary>
/// A production support incident (inheritance: an Incident is a WorkItem).
/// An incident is composed of its notes (composition).
/// </summary>
public class Incident : WorkItem
{
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; } = Priority.Medium;
    public IncidentStatus Status { get; set; } = IncidentStatus.New;

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;

    public int ReportedById { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public string ReportedByDepartment { get; set; } = string.Empty;

    public int? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }

    public string? RootCause { get; set; }
    public string? Resolution { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<IncidentNote> Notes { get; set; } = new();

    public override bool IsComplete =>
        Status is IncidentStatus.Resolved or IncidentStatus.Closed;

    /// <summary>SLA target by priority (typical tier-two support targets).</summary>
    public override TimeSpan GetSlaTarget() => Priority switch
    {
        Priority.Critical => TimeSpan.FromHours(4),
        Priority.High     => TimeSpan.FromHours(24),
        Priority.Medium   => TimeSpan.FromDays(3),
        _                 => TimeSpan.FromDays(10)
    };
}

public class IncidentNote
{
    public int NoteId { get; set; }
    public int IncidentId { get; set; }
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}
