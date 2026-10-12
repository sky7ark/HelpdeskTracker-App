/* =====================================================================
   HelpDesk Tracker - Sample data for demos
   ===================================================================== */
SET NOCOUNT ON;
GO

INSERT INTO dbo.Department (Name) VALUES
 (N'Information Technology'), (N'Finance'), (N'Retail Operations'), (N'Licensing'), (N'Human Resources');

INSERT INTO dbo.AppUser (FullName, Email, DepartmentId, IsSupportAgent) VALUES
 (N'Alex Morgan',   N'alex.morgan@example.com',   1, 1),
 (N'Priya Sharma',  N'priya.sharma@example.com',  1, 1),
 (N'Jordan Lee',    N'jordan.lee@example.com',    1, 1),
 (N'Sam Carter',    N'sam.carter@example.com',    2, 0),
 (N'Taylor Nguyen', N'taylor.nguyen@example.com', 3, 0),
 (N'Chris Dubois',  N'chris.dubois@example.com',  4, 0),
 (N'Morgan Fisher', N'morgan.fisher@example.com', 5, 0);

INSERT INTO dbo.Category (Name, ApplicationName) VALUES
 (N'Login / Access',     N'Licensing Portal'),
 (N'Application Error',  N'Licensing Portal'),
 (N'Data Correction',    N'Inventory System'),
 (N'Report Issue',       N'Inventory System'),
 (N'Hardware / Printer', N'Point of Sale'),
 (N'Transaction Error',  N'Point of Sale'),
 (N'Payroll Calculation',N'HR & Payroll');
GO

DECLARE @now DATETIME2(0) = SYSUTCDATETIME();

INSERT INTO dbo.Incident (Title, Description, CategoryId, Priority, Status, ReportedById, AssignedToId, RootCause, Resolution, CreatedAt, UpdatedAt, ResolvedAt) VALUES
 (N'Cannot log in to licensing portal',     N'Users in Licensing get "session expired" immediately after login.', 1, 1, 'InProgress', 6, 1, NULL, NULL, DATEADD(HOUR,-3,@now),  DATEADD(HOUR,-1,@now), NULL),
 (N'Store 14 POS terminal frozen',          N'Terminal 2 freezes when scanning a gift card.',                   6, 2, 'Assigned',   5, 2, NULL, NULL, DATEADD(HOUR,-20,@now), DATEADD(HOUR,-18,@now), NULL),
 (N'Stock count wrong for SKU 55821',       N'Inventory shows -4 on hand after a return was processed twice.',  3, 2, 'New',        5, NULL, NULL, NULL, DATEADD(HOUR,-6,@now),  DATEADD(HOUR,-6,@now), NULL),
 (N'Monthly sales report timing out',       N'Report runs longer than 5 minutes and fails for the full month.', 4, 3, 'OnHold',     4, 3, NULL, NULL, DATEADD(DAY,-4,@now),   DATEADD(DAY,-2,@now), NULL),
 (N'Receipt printer not printing logo',     N'Logo missing on receipts at two stores after driver update.',     5, 4, 'New',        5, NULL, NULL, NULL, DATEADD(DAY,-1,@now),   DATEADD(DAY,-1,@now), NULL),
 (N'Overtime not calculated for stat day',  N'Payroll did not apply stat holiday overtime rate.',              7, 1, 'Resolved',   7, 1,
    N'Stat holiday table missing the new holiday date for this year.',
    N'Added missing holiday row, re-ran payroll calculation for affected staff, confirmed with HR.',
    DATEADD(DAY,-6,@now), DATEADD(DAY,-5,@now), DATEADD(DAY,-5,@now)),
 (N'Error 500 when uploading licence PDF',  N'Upload fails for files over 4 MB.',                               2, 2, 'Closed',     6, 2,
    N'Web server request size limit (maxAllowedContentLength) was set to 4 MB.',
    N'Raised limit to 20 MB in web.config, tested upload, documented in KB-001.',
    DATEADD(DAY,-10,@now), DATEADD(DAY,-8,@now), DATEADD(DAY,-9,@now));

INSERT INTO dbo.IncidentNote (IncidentId, AuthorId, NoteText, IsInternal) VALUES
 (1000, 1, N'Reproduced in test. Auth cookie is rejected - checking server clock and cookie settings.', 1),
 (1000, 1, N'Told users we are working on it; workaround is to use a private browser window.', 0),
 (1005, 1, N'Holiday table fixed and payroll re-run. HR confirmed amounts.', 0);

INSERT INTO dbo.IncidentAudit (IncidentId, ChangedById, OldStatus, NewStatus)
SELECT IncidentId, ReportedById, NULL, 'New' FROM dbo.Incident;
GO
