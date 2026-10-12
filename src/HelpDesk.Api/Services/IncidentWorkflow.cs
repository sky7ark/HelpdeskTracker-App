using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

/// <summary>
/// Defines which status changes are allowed. Keeping the rules in one place
/// makes the impact of a future change easy to see and easy to test.
/// </summary>
public static class IncidentWorkflow
{
    private static readonly IReadOnlyDictionary<IncidentStatus, IncidentStatus[]> Allowed =
        new Dictionary<IncidentStatus, IncidentStatus[]>
        {
            [IncidentStatus.New]        = [IncidentStatus.Assigned, IncidentStatus.InProgress, IncidentStatus.OnHold, IncidentStatus.Resolved],
            [IncidentStatus.Assigned]   = [IncidentStatus.InProgress, IncidentStatus.OnHold, IncidentStatus.Resolved],
            [IncidentStatus.InProgress] = [IncidentStatus.Assigned, IncidentStatus.OnHold, IncidentStatus.Resolved],
            [IncidentStatus.OnHold]     = [IncidentStatus.Assigned, IncidentStatus.InProgress],
            [IncidentStatus.Resolved]   = [IncidentStatus.InProgress, IncidentStatus.Closed], // re-open or close
            [IncidentStatus.Closed]     = []
        };

    public static bool CanMove(IncidentStatus from, IncidentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<IncidentStatus> NextStatuses(IncidentStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];
}
