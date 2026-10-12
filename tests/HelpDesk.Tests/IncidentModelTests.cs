using HelpDesk.Api.Models;
using HelpDesk.Api.Services;

namespace HelpDesk.Tests;

public class IncidentModelTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(Priority.Critical, 4)]
    [InlineData(Priority.High, 24)]
    [InlineData(Priority.Medium, 72)]
    [InlineData(Priority.Low, 240)]
    public void SlaTarget_DependsOnPriority(Priority priority, int expectedHours)
    {
        var incident = new Incident { Priority = priority, CreatedAt = Created };

        Assert.Equal(TimeSpan.FromHours(expectedHours), incident.GetSlaTarget());
        Assert.Equal(Created.AddHours(expectedHours), incident.SlaDueAt);
    }

    [Fact]
    public void OpenIncident_PastDueDate_IsOverdue()
    {
        var incident = new Incident { Priority = Priority.Critical, CreatedAt = Created };

        Assert.False(incident.IsOverdue(Created.AddHours(3)));
        Assert.True(incident.IsOverdue(Created.AddHours(5)));
    }

    [Fact]
    public void ResolvedIncident_UsesResolvedTime_NotCurrentTime()
    {
        var incident = new Incident
        {
            Priority = Priority.Critical, CreatedAt = Created,
            Status = IncidentStatus.Resolved, ResolvedAt = Created.AddHours(2)
        };

        // Resolved inside the SLA, so it never becomes overdue later
        Assert.False(incident.IsOverdue(Created.AddDays(30)));
    }

    [Fact]
    public void Incident_IsAWorkItem()
    {
        WorkItem item = new Incident { Status = IncidentStatus.Closed };
        Assert.True(item.IsComplete);
    }

    [Theory]
    [InlineData(IncidentStatus.New, IncidentStatus.Assigned, true)]
    [InlineData(IncidentStatus.Resolved, IncidentStatus.Closed, true)]
    [InlineData(IncidentStatus.Resolved, IncidentStatus.InProgress, true)]
    [InlineData(IncidentStatus.New, IncidentStatus.Closed, false)]
    [InlineData(IncidentStatus.Closed, IncidentStatus.InProgress, false)]
    [InlineData(IncidentStatus.OnHold, IncidentStatus.Resolved, false)]
    public void Workflow_AllowsOnlyValidTransitions(IncidentStatus from, IncidentStatus to, bool expected)
    {
        Assert.Equal(expected, IncidentWorkflow.CanMove(from, to));
    }

    [Fact]
    public void Workflow_ClosedHasNoNextStatus()
    {
        Assert.Empty(IncidentWorkflow.NextStatuses(IncidentStatus.Closed));
    }
}
