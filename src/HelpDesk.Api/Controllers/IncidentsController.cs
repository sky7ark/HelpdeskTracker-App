using HelpDesk.Api.Models;
using HelpDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>REST web service for incidents. Used by the web page and usable by other systems.</summary>
[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _service;
    private readonly TimeProvider _clock;

    public IncidentsController(IIncidentService service, TimeProvider clock)
    {
        _service = service;
        _clock = clock;
    }

    /// <summary>GET api/incidents?status=New&amp;priority=Critical&amp;assignedToId=1&amp;search=login</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentListItem>>> Search(
        [FromQuery] IncidentStatus? status, [FromQuery] Priority? priority,
        [FromQuery] int? assignedToId, [FromQuery] string? search, CancellationToken ct)
    {
        return Ok(await _service.SearchAsync(new IncidentSearchCriteria(status, priority, assignedToId, search), ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IncidentDetail>> Get(int id, CancellationToken ct) =>
        Ok(await _service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateIncidentRequest request, CancellationToken ct)
    {
        int id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateStatusRequest request, CancellationToken ct)
    {
        await _service.UpdateStatusAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, AddNoteRequest request, CancellationToken ct)
    {
        await _service.AddNoteAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>GET api/incidents/export.xml - same filters as search, returned as an XML file.</summary>
    [HttpGet("export.xml")]
    public async Task<IActionResult> ExportXml(
        [FromQuery] IncidentStatus? status, [FromQuery] Priority? priority,
        [FromQuery] int? assignedToId, [FromQuery] string? search, CancellationToken ct)
    {
        var items = await _service.SearchAsync(new IncidentSearchCriteria(status, priority, assignedToId, search), ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var xml = XmlExportService.BuildIncidentExport(items, now);

        var body = xml.Declaration + Environment.NewLine + xml.ToString();
        return File(System.Text.Encoding.UTF8.GetBytes(body), "application/xml", $"incidents-{now:yyyyMMdd-HHmm}.xml");
    }
}
