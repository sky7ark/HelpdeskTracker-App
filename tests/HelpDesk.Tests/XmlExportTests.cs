using System.Xml.Linq;
using HelpDesk.Api.Models;
using HelpDesk.Api.Services;

namespace HelpDesk.Tests;

public class XmlExportTests
{
    private static readonly XNamespace Ns = XmlExportService.Namespace;
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static IncidentListItem Item(int id, string title, string? assignee = "Alex Morgan") => new(
        id, title, Priority.High, IncidentStatus.Assigned, "Login / Access", "Licensing Portal",
        "Sam Carter", assignee, Now.AddHours(-2), Now.AddHours(22), false);

    [Fact]
    public void Export_ContainsOneElementPerIncident()
    {
        var doc = XmlExportService.BuildIncidentExport([Item(1000, "A"), Item(1001, "B")], Now);

        Assert.Equal("2", doc.Root!.Attribute("count")!.Value);
        Assert.Equal(2, doc.Root.Elements(Ns + "Incident").Count());
        Assert.Equal("2026-10-10T12:00:00Z", doc.Root.Attribute("generatedAt")!.Value);
    }

    [Fact]
    public void Export_EscapesSpecialCharacters_AndRoundTrips()
    {
        var doc = XmlExportService.BuildIncidentExport([Item(1000, "Error <500> & timeout")], Now);

        var reparsed = XDocument.Parse(doc.ToString());
        Assert.Equal("Error <500> & timeout", reparsed.Root!.Element(Ns + "Incident")!.Element(Ns + "Title")!.Value);
    }

    [Fact]
    public void Export_OmitsAssignedTo_WhenUnassigned()
    {
        var doc = XmlExportService.BuildIncidentExport([Item(1000, "A", assignee: null)], Now);

        Assert.Null(doc.Root!.Element(Ns + "Incident")!.Element(Ns + "AssignedTo"));
    }
}
