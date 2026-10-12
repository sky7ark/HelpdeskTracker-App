using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelpDesk.Tests;

public class IncidentServiceTests
{
    private readonly InMemoryIncidentRepository _repo = new();
    private readonly IncidentService _service;

    public IncidentServiceTests()
    {
        _service = new IncidentService(_repo, new FixedTimeProvider(DateTime.UtcNow), NullLogger<IncidentService>.Instance);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsNewId()
    {
        int id = await _service.CreateAsync(new CreateIncidentRequest(
            "Printer offline", "Store printer shows offline status", 1, Priority.Low, 2));

        Assert.Equal(IncidentStatus.New, _repo.Incidents.Single(i => i.Id == id).Status);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateAsync(
            new CreateIncidentRequest("Printer offline", "Store printer shows offline", 99, Priority.Low, 2)));
    }

    [Fact]
    public async Task UpdateStatus_UnknownIncident_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateStatusAsync(
            12345, new UpdateStatusRequest(IncidentStatus.Assigned, 1, null, null, 1)));
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_IsRejected()
    {
        var incident = _repo.Add(IncidentStatus.New);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateStatusAsync(
            incident.Id, new UpdateStatusRequest(IncidentStatus.Closed, null, null, null, 1)));
        Assert.Contains("Cannot move", ex.Message);
    }

    [Fact]
    public async Task Assigning_WithoutAgent_IsRejected()
    {
        var incident = _repo.Add(IncidentStatus.New);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateStatusAsync(
            incident.Id, new UpdateStatusRequest(IncidentStatus.Assigned, null, null, null, 1)));
    }

    [Fact]
    public async Task Assigning_ToNonSupportUser_IsRejected()
    {
        var incident = _repo.Add(IncidentStatus.New);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateStatusAsync(
            incident.Id, new UpdateStatusRequest(IncidentStatus.Assigned, AssignedToId: 2, null, null, 1)));
    }

    [Fact]
    public async Task Resolving_WithoutRootCause_IsRejected()
    {
        var incident = _repo.Add(IncidentStatus.InProgress, assignedTo: 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateStatusAsync(
            incident.Id, new UpdateStatusRequest(IncidentStatus.Resolved, null, null, "Restarted service", 1)));
        Assert.Contains("root cause", ex.Message);
    }

    [Fact]
    public async Task Resolving_WithRootCauseAndResolution_Succeeds()
    {
        var incident = _repo.Add(IncidentStatus.InProgress, assignedTo: 1);

        await _service.UpdateStatusAsync(incident.Id, new UpdateStatusRequest(
            IncidentStatus.Resolved, null, "Expired certificate", "Renewed certificate", 1));

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("Expired certificate", incident.RootCause);
    }

    [Fact]
    public async Task AddNote_ToClosedIncident_IsRejected()
    {
        var incident = _repo.Add(IncidentStatus.Closed);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AddNoteAsync(
            incident.Id, new AddNoteRequest(1, "Following up")));
    }

    [Fact]
    public async Task Get_ReturnsAllowedNextStatuses()
    {
        var incident = _repo.Add(IncidentStatus.Resolved);

        var detail = await _service.GetAsync(incident.Id);

        Assert.Equal(new[] { IncidentStatus.InProgress, IncidentStatus.Closed }, detail.NextStatuses);
    }
}
