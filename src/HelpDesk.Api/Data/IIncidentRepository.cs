using HelpDesk.Api.Models;

namespace HelpDesk.Api.Data;

/// <summary>
/// Data access contract. The service layer depends on this interface, not on
/// SQL Server, so business rules can be unit tested with an in-memory fake.
/// </summary>
public interface IIncidentRepository
{
    Task<IReadOnlyList<Incident>> SearchAsync(IncidentSearchCriteria criteria, CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(int incidentId, CancellationToken ct = default);
    Task<int> CreateAsync(CreateIncidentRequest request, CancellationToken ct = default);
    Task UpdateStatusAsync(int incidentId, UpdateStatusRequest request, CancellationToken ct = default);
    Task AddNoteAsync(int incidentId, AddNoteRequest request, CancellationToken ct = default);
    Task<Lookups> GetLookupsAsync(CancellationToken ct = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
}
