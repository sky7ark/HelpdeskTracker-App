/* =====================================================================
   HelpDesk Tracker - Data integrity checks and correction scripts
   Typical tier-two support task: find bad data, find the cause, fix it
   safely. Each fix runs in a transaction and is ROLLED BACK by default -
   review the output, then change ROLLBACK to COMMIT to apply.
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- CHECK 1: Status says "Assigned/InProgress" but nobody is assigned ---------- */
SELECT IncidentId, Title, Status, AssignedToId
FROM   dbo.Incident
WHERE  Status IN ('Assigned','InProgress') AND AssignedToId IS NULL;

/* ---------- CHECK 2: Open incidents assigned to an inactive (departed) user ---------- */
SELECT i.IncidentId, i.Title, i.Status, u.FullName AS InactiveAssignee
FROM   dbo.Incident i
JOIN   dbo.AppUser u ON u.UserId = i.AssignedToId
WHERE  u.IsActive = 0 AND i.Status NOT IN ('Resolved','Closed');

/* ---------- CHECK 3: Probable duplicates (same reporter + title within 1 hour) ---------- */
SELECT a.IncidentId AS OriginalId, b.IncidentId AS DuplicateId, a.Title, a.ReportedById
FROM   dbo.Incident a
JOIN   dbo.Incident b
       ON  b.ReportedById = a.ReportedById
       AND b.Title        = a.Title
       AND b.IncidentId   > a.IncidentId
       AND b.CreatedAt   <= DATEADD(HOUR, 1, a.CreatedAt);

/* ---------- CHECK 4: Incidents with no audit history (created outside the app) ---------- */
SELECT i.IncidentId, i.Title, i.CreatedAt
FROM   dbo.Incident i
WHERE  NOT EXISTS (SELECT 1 FROM dbo.IncidentAudit a WHERE a.IncidentId = i.IncidentId);
GO

/* =====================================================================
   FIX for CHECK 1: move unassigned "Assigned/InProgress" incidents back to "New"
   ===================================================================== */
BEGIN TRANSACTION;

DECLARE @SystemUserId INT = 1;   -- support agent running the fix
DECLARE @Fixed TABLE (IncidentId INT, OldStatus VARCHAR(20));

UPDATE dbo.Incident
SET    Status = 'New', UpdatedAt = SYSUTCDATETIME()
OUTPUT deleted.IncidentId, deleted.Status INTO @Fixed
WHERE  Status IN ('Assigned','InProgress') AND AssignedToId IS NULL;

INSERT INTO dbo.IncidentAudit (IncidentId, ChangedById, OldStatus, NewStatus)
SELECT IncidentId, @SystemUserId, OldStatus, 'New' FROM @Fixed;

SELECT * FROM @Fixed;              -- review exactly which rows changed

ROLLBACK TRANSACTION;              -- change to COMMIT TRANSACTION when verified
GO
