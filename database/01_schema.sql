/* =====================================================================
   HelpDesk Tracker - Database schema (SQL Server 2019+)
   Run in SQL Server Management Studio (SSMS) against a new database:
       CREATE DATABASE HelpDeskTracker;
       GO
       USE HelpDeskTracker;
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- Drop in dependency order so the script can be re-run ---------- */
IF OBJECT_ID('dbo.vw_IncidentSummary', 'V') IS NOT NULL DROP VIEW dbo.vw_IncidentSummary;
IF OBJECT_ID('dbo.IncidentAudit', 'U')      IS NOT NULL DROP TABLE dbo.IncidentAudit;
IF OBJECT_ID('dbo.IncidentNote', 'U')       IS NOT NULL DROP TABLE dbo.IncidentNote;
IF OBJECT_ID('dbo.Incident', 'U')           IS NOT NULL DROP TABLE dbo.Incident;
IF OBJECT_ID('dbo.Category', 'U')           IS NOT NULL DROP TABLE dbo.Category;
IF OBJECT_ID('dbo.AppUser', 'U')            IS NOT NULL DROP TABLE dbo.AppUser;
IF OBJECT_ID('dbo.Department', 'U')         IS NOT NULL DROP TABLE dbo.Department;
GO

/* ---------- Lookup tables (normalized: no repeated text in Incident) ---------- */
CREATE TABLE dbo.Department
(
    DepartmentId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Department PRIMARY KEY,
    Name           NVARCHAR(100)     NOT NULL CONSTRAINT UQ_Department_Name UNIQUE
);

CREATE TABLE dbo.AppUser
(
    UserId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
    FullName       NVARCHAR(100)     NOT NULL,
    Email          NVARCHAR(200)     NOT NULL CONSTRAINT UQ_AppUser_Email UNIQUE,
    DepartmentId   INT               NOT NULL CONSTRAINT FK_AppUser_Department REFERENCES dbo.Department(DepartmentId),
    IsSupportAgent BIT               NOT NULL CONSTRAINT DF_AppUser_IsAgent DEFAULT (0),
    IsActive       BIT               NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1)
);

CREATE TABLE dbo.Category
(
    CategoryId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Category PRIMARY KEY,
    Name           NVARCHAR(100)     NOT NULL CONSTRAINT UQ_Category_Name UNIQUE,
    ApplicationName NVARCHAR(100)    NOT NULL   -- which business application the category belongs to
);
GO

/* ---------- Core tables ---------- */
CREATE TABLE dbo.Incident
(
    IncidentId     INT IDENTITY(1000,1) NOT NULL CONSTRAINT PK_Incident PRIMARY KEY,
    Title          NVARCHAR(200)     NOT NULL,
    Description    NVARCHAR(MAX)     NOT NULL,
    CategoryId     INT               NOT NULL CONSTRAINT FK_Incident_Category   REFERENCES dbo.Category(CategoryId),
    Priority       TINYINT           NOT NULL CONSTRAINT CK_Incident_Priority   CHECK (Priority BETWEEN 1 AND 4), -- 1 = Critical ... 4 = Low
    Status         VARCHAR(20)       NOT NULL CONSTRAINT DF_Incident_Status     DEFAULT ('New')
                                     CONSTRAINT CK_Incident_Status CHECK (Status IN ('New','Assigned','InProgress','OnHold','Resolved','Closed')),
    ReportedById   INT               NOT NULL CONSTRAINT FK_Incident_Reporter   REFERENCES dbo.AppUser(UserId),
    AssignedToId   INT               NULL     CONSTRAINT FK_Incident_Assignee   REFERENCES dbo.AppUser(UserId),
    RootCause      NVARCHAR(1000)    NULL,
    Resolution     NVARCHAR(1000)    NULL,
    CreatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_Incident_CreatedAt  DEFAULT (SYSUTCDATETIME()),
    UpdatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_Incident_UpdatedAt  DEFAULT (SYSUTCDATETIME()),
    ResolvedAt     DATETIME2(0)      NULL,
    /* Data-integrity rule enforced by the database itself */
    CONSTRAINT CK_Incident_ResolvedData CHECK
    (
        (Status IN ('Resolved','Closed') AND ResolvedAt IS NOT NULL AND Resolution IS NOT NULL)
        OR (Status NOT IN ('Resolved','Closed'))
    )
);

CREATE TABLE dbo.IncidentNote
(
    NoteId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IncidentNote PRIMARY KEY,
    IncidentId     INT               NOT NULL CONSTRAINT FK_IncidentNote_Incident REFERENCES dbo.Incident(IncidentId) ON DELETE CASCADE,
    AuthorId       INT               NOT NULL CONSTRAINT FK_IncidentNote_Author   REFERENCES dbo.AppUser(UserId),
    NoteText       NVARCHAR(2000)    NOT NULL,
    IsInternal     BIT               NOT NULL CONSTRAINT DF_IncidentNote_Internal DEFAULT (1),
    CreatedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_IncidentNote_Created  DEFAULT (SYSUTCDATETIME())
);

/* Every status change is recorded - supports root cause analysis and auditing */
CREATE TABLE dbo.IncidentAudit
(
    AuditId        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IncidentAudit PRIMARY KEY,
    IncidentId     INT               NOT NULL CONSTRAINT FK_IncidentAudit_Incident REFERENCES dbo.Incident(IncidentId) ON DELETE CASCADE,
    ChangedById    INT               NOT NULL CONSTRAINT FK_IncidentAudit_User     REFERENCES dbo.AppUser(UserId),
    OldStatus      VARCHAR(20)       NULL,
    NewStatus      VARCHAR(20)       NOT NULL,
    ChangedAt      DATETIME2(0)      NOT NULL CONSTRAINT DF_IncidentAudit_ChangedAt DEFAULT (SYSUTCDATETIME())
);
GO

/* ---------- Indexes for the most common queries ---------- */
CREATE NONCLUSTERED INDEX IX_Incident_Status_Priority ON dbo.Incident (Status, Priority) INCLUDE (Title, AssignedToId, CreatedAt);
CREATE NONCLUSTERED INDEX IX_Incident_AssignedToId    ON dbo.Incident (AssignedToId) WHERE AssignedToId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_Incident_CategoryId      ON dbo.Incident (CategoryId);
CREATE NONCLUSTERED INDEX IX_IncidentNote_IncidentId  ON dbo.IncidentNote (IncidentId);
CREATE NONCLUSTERED INDEX IX_IncidentAudit_IncidentId ON dbo.IncidentAudit (IncidentId);
GO

/* ---------- View: flattened incident list used by the API and reporting ---------- */
CREATE VIEW dbo.vw_IncidentSummary
AS
SELECT  i.IncidentId,
        i.Title,
        i.Description,
        i.Priority,
        i.Status,
        i.CategoryId,
        c.Name            AS CategoryName,
        c.ApplicationName,
        i.ReportedById,
        r.FullName        AS ReportedByName,
        d.Name            AS ReportedByDepartment,
        i.AssignedToId,
        a.FullName        AS AssignedToName,
        i.RootCause,
        i.Resolution,
        i.CreatedAt,
        i.UpdatedAt,
        i.ResolvedAt
FROM    dbo.Incident   i
JOIN    dbo.Category   c ON c.CategoryId   = i.CategoryId
JOIN    dbo.AppUser    r ON r.UserId       = i.ReportedById
JOIN    dbo.Department d ON d.DepartmentId = r.DepartmentId
LEFT JOIN dbo.AppUser  a ON a.UserId       = i.AssignedToId;
GO
