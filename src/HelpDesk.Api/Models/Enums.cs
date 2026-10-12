namespace HelpDesk.Api.Models;

/// <summary>Incident priority. Values match the CHECK constraint in the database (1-4).</summary>
public enum Priority : byte
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4
}

/// <summary>Incident lifecycle states. Names match the Status column values.</summary>
public enum IncidentStatus
{
    New,
    Assigned,
    InProgress,
    OnHold,
    Resolved,
    Closed
}
