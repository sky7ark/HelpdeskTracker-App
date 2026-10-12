namespace HelpDesk.Api.Models;

/// <summary>
/// Base class for anything the support team tracks (abstraction).
/// Shared behaviour such as SLA tracking lives here; each kind of work item
/// decides its own SLA target by overriding <see cref="GetSlaTarget"/>.
/// </summary>
public abstract class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>How long the team has to resolve this item.</summary>
    public abstract TimeSpan GetSlaTarget();

    /// <summary>Whether work on the item is finished.</summary>
    public abstract bool IsComplete { get; }

    public DateTime SlaDueAt => CreatedAt + GetSlaTarget();

    /// <summary>True when the item missed (or is missing) its SLA target.</summary>
    public bool IsOverdue(DateTime utcNow)
    {
        var finishedOrNow = IsComplete && ResolvedAt.HasValue ? ResolvedAt.Value : utcNow;
        return finishedOrNow > SlaDueAt;
    }
}
