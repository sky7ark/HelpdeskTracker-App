using HelpDesk.Api.Data;
using HelpDesk.Api.Models;

namespace HelpDesk.Tests;

/// <summary>In-memory stand-in for SQL Server so business rules can be tested in isolation.</summary>
public sealed class InMemoryIncidentRepository : IIncidentRepository
{
    public List<Incident> Incidents { get; } = new();
    public List<CategoryLookup> Categories { get; } = [new(1, "Login / Access", "Licensing Portal")];
    public List<UserLookup> Users { get; } =
    [
        new(1, "Alex Morgan", IsSupportAgent: true),
        new(2, "Sam Carter", IsSupportAgent: false)
    ];

    private int _nextId = 1000;

    public Incident Add(IncidentStatus status, Priority priority = Priority.Medium, int? assignedTo = null)
    {
        var incident = new Incident
        {
            Id = _nextId++, Title = "Test incident", Description = "Something broke",
            Status = status, Priority = priority, AssignedToId = assignedTo,
            CategoryId = 1, ReportedById = 2, CreatedAt = DateTime.UtcNow
        };
        Incidents.Add(incident);
        return incident;
    }

    public Task<IReadOnlyList<Incident>> SearchAsync(IncidentSearchCriteria c, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Incident>>(Incidents
            .Where(i => c.Status is null || i.Status == c.Status)
            .Where(i => c.Priority is null || i.Priority == c.Priority)
            .ToList());

    public Task<Incident?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Incidents.FirstOrDefault(i => i.Id == id));

    public Task<int> CreateAsync(CreateIncidentRequest r, CancellationToken ct = default)
    {
        var incident = Add(IncidentStatus.New, r.Priority);
        incident.Title = r.Title;
        return Task.FromResult(incident.Id);
    }

    public Task UpdateStatusAsync(int id, UpdateStatusRequest r, CancellationToken ct = default)
    {
        var i = Incidents.Single(x => x.Id == id);
        i.Status = r.NewStatus;
        i.AssignedToId = r.AssignedToId ?? i.AssignedToId;
        i.RootCause = r.RootCause ?? i.RootCause;
        i.Resolution = r.Resolution ?? i.Resolution;
        return Task.CompletedTask;
    }

    public Task AddNoteAsync(int id, AddNoteRequest r, CancellationToken ct = default)
    {
        Incidents.Single(x => x.Id == id).Notes.Add(new IncidentNote { AuthorId = r.AuthorId, NoteText = r.NoteText });
        return Task.CompletedTask;
    }

    public Task<Lookups> GetLookupsAsync(CancellationToken ct = default) =>
        Task.FromResult(new Lookups(Categories, Users));

    public Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var open = Incidents.Where(i => !i.IsComplete).ToList();
        var resolved = Incidents.Where(i => i.ResolvedAt.HasValue).ToList();
        return Task.FromResult(new DashboardStats(
            open.Count,
            open.Count(i => i.Priority == Priority.Critical),
            open.Count(i => i.Status == IncidentStatus.New && i.AssignedToId is null),
            resolved.Count(i => i.ResolvedAt >= DateTime.UtcNow.AddDays(-7)),
            resolved.Count == 0 ? null : Math.Round((decimal)resolved.Average(i => (i.ResolvedAt!.Value - i.CreatedAt).TotalHours), 1),
            open.GroupBy(i => i.ApplicationName).Select(g => new ApplicationLoad(g.Key, g.Count())).ToList()));
    }
}

/// <summary>A clock the test controls, so SLA calculations are predictable.</summary>
public sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}
