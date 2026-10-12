using System.Globalization;
using System.Xml.Linq;
using HelpDesk.Api.Models;

namespace HelpDesk.Api.Services;

/// <summary>
/// Builds an XML extract of incidents, the kind of file often exchanged with
/// external vendors or loaded into a reporting system.
/// </summary>
public static class XmlExportService
{
    public const string Namespace = "urn:helpdesk:incidents:v1";

    public static XDocument BuildIncidentExport(IEnumerable<IncidentListItem> incidents, DateTime generatedAtUtc)
    {
        XNamespace ns = Namespace;
        var list = incidents.ToList();

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "IncidentExport",
                new XAttribute("generatedAt", ToIso(generatedAtUtc)),
                new XAttribute("count", list.Count),
                list.Select(i =>
                    new XElement(ns + "Incident",
                        new XAttribute("id", i.Id),
                        new XAttribute("priority", i.Priority),
                        new XAttribute("status", i.Status),
                        new XAttribute("overdue", i.IsOverdue ? "true" : "false"),
                        new XElement(ns + "Title", i.Title),           // XElement escapes <, & etc. automatically
                        new XElement(ns + "Application", i.ApplicationName),
                        new XElement(ns + "Category", i.CategoryName),
                        new XElement(ns + "ReportedBy", i.ReportedByName),
                        i.AssignedToName is null ? null : new XElement(ns + "AssignedTo", i.AssignedToName),
                        new XElement(ns + "CreatedAt", ToIso(i.CreatedAt)),
                        new XElement(ns + "SlaDueAt", ToIso(i.SlaDueAt))))));
    }

    private static string ToIso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
