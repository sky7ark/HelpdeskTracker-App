using System.Data;
using System.Data.Common;
using HelpDesk.Api.Models;

namespace HelpDesk.Api.Data;

/// <summary>
/// SQL Server implementation of <see cref="IIncidentRepository"/>.
/// Uses ADO.NET and stored procedures only - every value is passed as a
/// parameter, so user input can never change the SQL being run.
/// </summary>
public sealed class SqlIncidentRepository : IIncidentRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlIncidentRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<Incident>> SearchAsync(IncidentSearchCriteria c, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Incident_Search");
        AddParam(cmd, "@Status", c.Status?.ToString(), DbType.AnsiString);
        AddParam(cmd, "@Priority", c.Priority is null ? null : (byte)c.Priority, DbType.Byte);
        AddParam(cmd, "@AssignedToId", c.AssignedToId, DbType.Int32);
        AddParam(cmd, "@SearchText", string.IsNullOrWhiteSpace(c.SearchText) ? null : c.SearchText.Trim(), DbType.String);

        await conn.OpenAsync(ct);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<Incident>();
        while (await reader.ReadAsync(ct))
            results.Add(MapIncident(reader));
        return results;
    }

    public async Task<Incident?> GetByIdAsync(int incidentId, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Incident_GetById");
        AddParam(cmd, "@IncidentId", incidentId, DbType.Int32);

        await conn.OpenAsync(ct);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        var incident = MapIncident(reader);

        // Second result set: the incident's notes
        await reader.NextResultAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            incident.Notes.Add(new IncidentNote
            {
                NoteId = reader.GetInt32(reader.GetOrdinal("NoteId")),
                IncidentId = reader.GetInt32(reader.GetOrdinal("IncidentId")),
                AuthorId = reader.GetInt32(reader.GetOrdinal("AuthorId")),
                AuthorName = reader.GetString(reader.GetOrdinal("AuthorName")),
                NoteText = reader.GetString(reader.GetOrdinal("NoteText")),
                IsInternal = reader.GetBoolean(reader.GetOrdinal("IsInternal")),
                CreatedAt = AsUtc(reader.GetDateTime(reader.GetOrdinal("CreatedAt")))
            });
        }
        return incident;
    }

    public async Task<int> CreateAsync(CreateIncidentRequest r, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Incident_Create");
        AddParam(cmd, "@Title", r.Title.Trim(), DbType.String);
        AddParam(cmd, "@Description", r.Description.Trim(), DbType.String);
        AddParam(cmd, "@CategoryId", r.CategoryId, DbType.Int32);
        AddParam(cmd, "@Priority", (byte)r.Priority, DbType.Byte);
        AddParam(cmd, "@ReportedById", r.ReportedById, DbType.Int32);
        var newId = AddParam(cmd, "@NewIncidentId", null, DbType.Int32, ParameterDirection.Output);

        await conn.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
        return Convert.ToInt32(newId.Value);
    }

    public async Task UpdateStatusAsync(int incidentId, UpdateStatusRequest r, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Incident_UpdateStatus");
        AddParam(cmd, "@IncidentId", incidentId, DbType.Int32);
        AddParam(cmd, "@NewStatus", r.NewStatus.ToString(), DbType.AnsiString);
        AddParam(cmd, "@AssignedToId", r.AssignedToId, DbType.Int32);
        AddParam(cmd, "@RootCause", r.RootCause?.Trim(), DbType.String);
        AddParam(cmd, "@Resolution", r.Resolution?.Trim(), DbType.String);
        AddParam(cmd, "@ChangedById", r.ChangedById, DbType.Int32);

        await conn.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddNoteAsync(int incidentId, AddNoteRequest r, CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Incident_AddNote");
        AddParam(cmd, "@IncidentId", incidentId, DbType.Int32);
        AddParam(cmd, "@AuthorId", r.AuthorId, DbType.Int32);
        AddParam(cmd, "@NoteText", r.NoteText.Trim(), DbType.String);
        AddParam(cmd, "@IsInternal", r.IsInternal, DbType.Boolean);

        await conn.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Lookups> GetLookupsAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Lookup_Get");
        await conn.OpenAsync(ct);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var categories = new List<CategoryLookup>();
        while (await reader.ReadAsync(ct))
            categories.Add(new CategoryLookup(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

        await reader.NextResultAsync(ct);
        var users = new List<UserLookup>();
        while (await reader.ReadAsync(ct))
            users.Add(new UserLookup(reader.GetInt32(0), reader.GetString(1), reader.GetBoolean(2)));

        return new Lookups(categories, users);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        await using var conn = _factory.CreateConnection();
        await using var cmd = CreateProcedure(conn, "dbo.usp_Dashboard_GetStats");
        await conn.OpenAsync(ct);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        await reader.ReadAsync(ct);
        int open = IntOrZero(reader, "OpenCount");
        int critical = IntOrZero(reader, "CriticalOpen");
        int unassigned = IntOrZero(reader, "UnassignedCount");
        int resolved7 = IntOrZero(reader, "ResolvedLast7Days");
        int avgOrdinal = reader.GetOrdinal("AvgHoursToResolve");
        decimal? avgHours = reader.IsDBNull(avgOrdinal) ? null : reader.GetDecimal(avgOrdinal);

        await reader.NextResultAsync(ct);
        var byApp = new List<ApplicationLoad>();
        while (await reader.ReadAsync(ct))
            byApp.Add(new ApplicationLoad(reader.GetString(0), reader.GetInt32(1)));

        return new DashboardStats(open, critical, unassigned, resolved7, avgHours, byApp);
    }

    // ---------------- helpers ----------------

    private static DbCommand CreateProcedure(DbConnection conn, string procedureName)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = procedureName;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;
        return cmd;
    }

    private static DbParameter AddParam(DbCommand cmd, string name, object? value, DbType type,
        ParameterDirection direction = ParameterDirection.Input)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        p.Direction = direction;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return p;
    }

    private static Incident MapIncident(DbDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("IncidentId")),
        Title = r.GetString(r.GetOrdinal("Title")),
        Description = r.GetString(r.GetOrdinal("Description")),
        Priority = (Priority)r.GetByte(r.GetOrdinal("Priority")),
        Status = Enum.Parse<IncidentStatus>(r.GetString(r.GetOrdinal("Status"))),
        CategoryId = r.GetInt32(r.GetOrdinal("CategoryId")),
        CategoryName = r.GetString(r.GetOrdinal("CategoryName")),
        ApplicationName = r.GetString(r.GetOrdinal("ApplicationName")),
        ReportedById = r.GetInt32(r.GetOrdinal("ReportedById")),
        ReportedByName = r.GetString(r.GetOrdinal("ReportedByName")),
        ReportedByDepartment = r.GetString(r.GetOrdinal("ReportedByDepartment")),
        AssignedToId = NullableInt(r, "AssignedToId"),
        AssignedToName = NullableString(r, "AssignedToName"),
        RootCause = NullableString(r, "RootCause"),
        Resolution = NullableString(r, "Resolution"),
        CreatedAt = AsUtc(r.GetDateTime(r.GetOrdinal("CreatedAt"))),
        UpdatedAt = AsUtc(r.GetDateTime(r.GetOrdinal("UpdatedAt"))),
        ResolvedAt = NullableDate(r, "ResolvedAt")
    };

    // SQL Server stores UTC (SYSUTCDATETIME); mark it so JSON is sent with a "Z" suffix.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? NullableString(DbDataReader r, string column)
    {
        int i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    private static int? NullableInt(DbDataReader r, string column)
    {
        int i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    private static DateTime? NullableDate(DbDataReader r, string column)
    {
        int i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : AsUtc(r.GetDateTime(i));
    }

    private static int IntOrZero(DbDataReader r, string column)
    {
        int i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? 0 : r.GetInt32(i);
    }
}
