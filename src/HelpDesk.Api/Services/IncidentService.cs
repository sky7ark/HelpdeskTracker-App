using HelpDesk.Api.Data;
using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentListItem>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken ct = default);
    Task<IncidentDetail> GetAsync(int incidentId, CancellationToken ct = default);
    Task<int> CreateAsync(CreateIncidentRequest request, CancellationToken ct = default);
    Task UpdateStatusAsync(int incidentId, UpdateStatusRequest request, CancellationToken ct = default);
    Task AddNoteAsync(int incidentId, AddNoteRequest request, CancellationToken ct = default);
}

/// <summary>
/// Business rules for incidents. Controllers stay thin; the database stays
/// focused on storage. This class is where "what is allowed" is decided.
/// </summary>
public sealed class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _repository;
    private readonly TimeProvider _clock;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(IIncidentRepository repository, TimeProvider clock, ILogger<IncidentService> logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IncidentListItem>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var incidents = await _repository.SearchAsync(criteria, ct);
        return incidents.Select(i => IncidentListItem.From(i, now)).ToList();
    }

    public async Task<IncidentDetail> GetAsync(int incidentId, CancellationToken ct = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException($"Incident {incidentId} was not found.");

        return new IncidentDetail(
            incident,
            incident.IsOverdue(_clock.GetUtcNow().UtcDateTime),
            IncidentWorkflow.NextStatuses(incident.Status));
    }

    public async Task<int> CreateAsync(CreateIncidentRequest request, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Priority))
            throw new BusinessRuleException("Priority must be between 1 (Critical) and 4 (Low).");

        var lookups = await _repository.GetLookupsAsync(ct);
        if (lookups.Categories.All(c => c.CategoryId != request.CategoryId))
            throw new BusinessRuleException($"Category {request.CategoryId} does not exist.");
        if (lookups.Users.All(u => u.UserId != request.ReportedById))
            throw new BusinessRuleException($"User {request.ReportedById} does not exist or is inactive.");

        int id = await _repository.CreateAsync(request, ct);
        _logger.LogInformation("Incident {IncidentId} created with priority {Priority}", id, request.Priority);
        return id;
    }

    public async Task UpdateStatusAsync(int incidentId, UpdateStatusRequest request, CancellationToken ct = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException($"Incident {incidentId} was not found.");

        if (!IncidentWorkflow.CanMove(incident.Status, request.NewStatus))
            throw new BusinessRuleException(
                $"Cannot move an incident from {incident.Status} to {request.NewStatus}. " +
                $"Allowed: {string.Join(", ", IncidentWorkflow.NextStatuses(incident.Status))}.");

        var lookups = await _repository.GetLookupsAsync(ct);

        // An incident being worked on must have an owner
        int? assignee = request.AssignedToId ?? incident.AssignedToId;
        if (request.NewStatus is IncidentStatus.Assigned or IncidentStatus.InProgress && assignee is null)
            throw new BusinessRuleException("Choose a support agent before assigning or starting work.");

        if (request.AssignedToId is int newAssignee &&
            !lookups.Users.Any(u => u.UserId == newAssignee && u.IsSupportAgent))
            throw new BusinessRuleException("Incidents can only be assigned to an active support agent.");

        // Resolving requires the cause and the fix so the knowledge is not lost
        if (request.NewStatus == IncidentStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(request.RootCause ?? incident.RootCause))
                throw new BusinessRuleException("Enter the root cause before resolving.");
            if (string.IsNullOrWhiteSpace(request.Resolution ?? incident.Resolution))
                throw new BusinessRuleException("Enter the resolution before resolving.");
        }

        await _repository.UpdateStatusAsync(incidentId, request, ct);
        _logger.LogInformation("Incident {IncidentId} moved {OldStatus} -> {NewStatus} by user {UserId}",
            incidentId, incident.Status, request.NewStatus, request.ChangedById);
    }

    public async Task AddNoteAsync(int incidentId, AddNoteRequest request, CancellationToken ct = default)
    {
        var incident = await _repository.GetByIdAsync(incidentId, ct)
            ?? throw new NotFoundException($"Incident {incidentId} was not found.");

        if (incident.Status == IncidentStatus.Closed)
            throw new BusinessRuleException("Closed incidents cannot be changed. Create a new incident instead.");

        await _repository.AddNoteAsync(incidentId, request, ct);
    }
}
