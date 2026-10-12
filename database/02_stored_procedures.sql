/* =====================================================================
   HelpDesk Tracker - Stored procedures
   All data access from the C# API goes through these procedures
   (parameterized, no dynamic SQL, so they are safe from SQL injection).
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- Lookups for drop-down lists ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Lookup_Get
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CategoryId, Name, ApplicationName
    FROM   dbo.Category
    ORDER BY ApplicationName, Name;

    SELECT UserId, FullName, IsSupportAgent
    FROM   dbo.AppUser
    WHERE  IsActive = 1
    ORDER BY FullName;
END;
GO

/* ---------- Search incidents with optional filters ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Incident_Search
    @Status       VARCHAR(20)   = NULL,
    @Priority     TINYINT       = NULL,
    @AssignedToId INT           = NULL,
    @SearchText   NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (200) *
    FROM   dbo.vw_IncidentSummary
    WHERE  (@Status       IS NULL OR Status       = @Status)
      AND  (@Priority     IS NULL OR Priority     = @Priority)
      AND  (@AssignedToId IS NULL OR AssignedToId = @AssignedToId)
      AND  (@SearchText   IS NULL OR Title LIKE N'%' + @SearchText + N'%'
                                  OR Description LIKE N'%' + @SearchText + N'%')
    ORDER BY Priority, CreatedAt DESC
    OPTION (RECOMPILE);  -- avoids a poor cached plan for "catch-all" filters
END;
GO

/* ---------- Get one incident with its notes ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Incident_GetById
    @IncidentId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM dbo.vw_IncidentSummary WHERE IncidentId = @IncidentId;

    SELECT n.NoteId, n.IncidentId, n.AuthorId, u.FullName AS AuthorName,
           n.NoteText, n.IsInternal, n.CreatedAt
    FROM   dbo.IncidentNote n
    JOIN   dbo.AppUser u ON u.UserId = n.AuthorId
    WHERE  n.IncidentId = @IncidentId
    ORDER BY n.CreatedAt;
END;
GO

/* ---------- Create an incident ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Incident_Create
    @Title        NVARCHAR(200),
    @Description  NVARCHAR(MAX),
    @CategoryId   INT,
    @Priority     TINYINT,
    @ReportedById INT,
    @NewIncidentId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.Incident (Title, Description, CategoryId, Priority, ReportedById)
        VALUES (@Title, @Description, @CategoryId, @Priority, @ReportedById);

        SET @NewIncidentId = SCOPE_IDENTITY();

        INSERT INTO dbo.IncidentAudit (IncidentId, ChangedById, OldStatus, NewStatus)
        VALUES (@NewIncidentId, @ReportedById, NULL, 'New');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ---------- Assign / change status (with audit trail) ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Incident_UpdateStatus
    @IncidentId   INT,
    @NewStatus    VARCHAR(20),
    @AssignedToId INT            = NULL,
    @RootCause    NVARCHAR(1000) = NULL,
    @Resolution   NVARCHAR(1000) = NULL,
    @ChangedById  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OldStatus VARCHAR(20);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- UPDLOCK prevents two agents changing the same incident at the same time
        SELECT @OldStatus = Status
        FROM   dbo.Incident WITH (UPDLOCK, ROWLOCK)
        WHERE  IncidentId = @IncidentId;

        IF @OldStatus IS NULL
            THROW 50001, 'Incident not found.', 1;

        UPDATE dbo.Incident
        SET    Status       = @NewStatus,
               AssignedToId = COALESCE(@AssignedToId, AssignedToId),
               RootCause    = COALESCE(@RootCause, RootCause),
               Resolution   = COALESCE(@Resolution, Resolution),
               ResolvedAt   = CASE WHEN @NewStatus IN ('Resolved','Closed')
                                   THEN COALESCE(ResolvedAt, SYSUTCDATETIME())
                                   ELSE NULL END,
               UpdatedAt    = SYSUTCDATETIME()
        WHERE  IncidentId   = @IncidentId;

        INSERT INTO dbo.IncidentAudit (IncidentId, ChangedById, OldStatus, NewStatus)
        VALUES (@IncidentId, @ChangedById, @OldStatus, @NewStatus);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ---------- Add a note to an incident ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Incident_AddNote
    @IncidentId INT,
    @AuthorId   INT,
    @NoteText   NVARCHAR(2000),
    @IsInternal BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Incident WHERE IncidentId = @IncidentId)
        THROW 50001, 'Incident not found.', 1;

    INSERT INTO dbo.IncidentNote (IncidentId, AuthorId, NoteText, IsInternal)
    VALUES (@IncidentId, @AuthorId, @NoteText, @IsInternal);

    UPDATE dbo.Incident SET UpdatedAt = SYSUTCDATETIME() WHERE IncidentId = @IncidentId;
END;
GO

/* ---------- Dashboard numbers ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_GetStats
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        OpenCount       = SUM(CASE WHEN Status NOT IN ('Resolved','Closed') THEN 1 ELSE 0 END),
        CriticalOpen    = SUM(CASE WHEN Status NOT IN ('Resolved','Closed') AND Priority = 1 THEN 1 ELSE 0 END),
        UnassignedCount = SUM(CASE WHEN Status = 'New' AND AssignedToId IS NULL THEN 1 ELSE 0 END),
        ResolvedLast7Days = SUM(CASE WHEN ResolvedAt >= DATEADD(DAY, -7, SYSUTCDATETIME()) THEN 1 ELSE 0 END),
        AvgHoursToResolve = CAST(AVG(CASE WHEN ResolvedAt IS NOT NULL
                                          THEN DATEDIFF(MINUTE, CreatedAt, ResolvedAt) / 60.0 END) AS DECIMAL(8,1))
    FROM dbo.Incident;

    -- Open incidents per business application (where the support load is)
    SELECT c.ApplicationName, COUNT(*) AS OpenCount
    FROM   dbo.Incident i
    JOIN   dbo.Category c ON c.CategoryId = i.CategoryId
    WHERE  i.Status NOT IN ('Resolved','Closed')
    GROUP BY c.ApplicationName
    ORDER BY OpenCount DESC;
END;
GO
