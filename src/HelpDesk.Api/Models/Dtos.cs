using System.ComponentModel.DataAnnotations;

namespace HelpDesk.Api.Models;

// ---------- Requests coming in from the web page ----------

public record CreateIncidentRequest(
    [Required, StringLength(200, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Description,
    [Range(1, int.MaxValue)] int CategoryId,
    [EnumDataType(typeof(Priority))] Priority Priority,
    [Range(1, int.MaxValue)] int ReportedById);

public record UpdateStatusRequest(
    IncidentStatus NewStatus,
    int? AssignedToId,
    [StringLength(1000)] string? RootCause,
    [StringLength(1000)] string? Resolution,
    [Range(1, int.MaxValue)] int ChangedById);

public record AddNoteRequest(
    [Range(1, int.MaxValue)] int AuthorId,
    [Required, StringLength(2000, MinimumLength = 1)] string NoteText,
    bool IsInternal = true);

public record IncidentSearchCriteria(
    IncidentStatus? Status = null,
    Priority? Priority = null,
    int? AssignedToId = null,
    string? SearchText = null);

// ---------- Responses going back to the web page ----------

public record IncidentListItem(
    int Id, string Title, Priority Priority, IncidentStatus Status,
    string CategoryName, string ApplicationName, string ReportedByName,
    string? AssignedToName, DateTime CreatedAt, DateTime SlaDueAt, bool IsOverdue)
{
    public static IncidentListItem From(Incident i, DateTime utcNow) => new(
        i.Id, i.Title, i.Priority, i.Status, i.CategoryName, i.ApplicationName,
        i.ReportedByName, i.AssignedToName, i.CreatedAt, i.SlaDueAt, i.IsOverdue(utcNow));
}

public record IncidentDetail(Incident Incident, bool IsOverdue, IReadOnlyList<IncidentStatus> NextStatuses);

public record CategoryLookup(int CategoryId, string Name, string ApplicationName);
public record UserLookup(int UserId, string FullName, bool IsSupportAgent);
public record Lookups(IReadOnlyList<CategoryLookup> Categories, IReadOnlyList<UserLookup> Users);

public record ApplicationLoad(string ApplicationName, int OpenCount);
public record DashboardStats(
    int OpenCount, int CriticalOpen, int UnassignedCount, int ResolvedLast7Days,
    decimal? AvgHoursToResolve, IReadOnlyList<ApplicationLoad> ByApplication);
