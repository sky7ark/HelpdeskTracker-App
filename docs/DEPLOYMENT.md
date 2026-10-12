# Deployment plan: IIS on Windows Server 2022/2025

An implementation plan for releasing HelpDesk Tracker to a Windows Server, as it would be
reviewed with the infrastructure and DBA teams before a change window.

## 1. Prerequisites (one-time server setup)

| Item | Owner | Notes |
|---|---|---|
| IIS role with the *Web Server* and *Management Console* features | Infrastructure | Server Manager → Add Roles and Features |
| ASP.NET Core 10 **Hosting Bundle** | Infrastructure | Installs the ASP.NET Core Module (ANCM) for IIS. Restart IIS afterwards (`iisreset`). |
| SQL Server login for the app pool identity | DBA | Least privilege: `EXECUTE` on the `dbo` schema only, no direct table access |
| TLS certificate bound to the site | Infrastructure | HTTPS only |

Grant the database permissions (run by the DBA):
```sql
CREATE LOGIN [DOMAIN\svc-helpdesk] FROM WINDOWS;
USE HelpDeskTracker;
CREATE USER [DOMAIN\svc-helpdesk] FOR LOGIN [DOMAIN\svc-helpdesk];
GRANT EXECUTE ON SCHEMA::dbo TO [DOMAIN\svc-helpdesk];   -- stored procedures only
```

## 2. Release steps

1. **Back up the database** before any schema change (`BACKUP DATABASE HelpDeskTracker TO DISK = ...`).
2. **Run the database scripts** in order. `02_stored_procedures.sql` uses `CREATE OR ALTER`, so it is safe to re-run.
3. **Publish the application** from Visual Studio (*Publish → Folder*) or from the command line:
   `dotnet publish src/HelpDesk.Api -c Release -o C:\Deploy\HelpDesk`
   The publish output includes the `web.config` that IIS needs.
4. **Create the IIS site.** Use an application pool with *.NET CLR version = No Managed Code*, and
   set its identity to the service account (`DOMAIN\svc-helpdesk`).
5. **Set the connection string** with an environment variable on the server, not in source control:
   `ConnectionStrings__HelpDesk = Server=SQLPROD01;Database=HelpDeskTracker;Trusted_Connection=True;Encrypt=True;`
6. **Smoke test.** Check that `GET /api/health` returns `OK`, the dashboard loads, an incident can be
   created and resolved in a test record, and the XML export downloads.

## 3. Rollback plan

- **Application:** keep the previous publish folder. Point the IIS site back to it and recycle the app pool.
- **Database:** stored procedure changes roll back by re-running the previous version of the script.
  Schema changes roll back by restoring the backup taken in step 1.

## 4. Impact assessment checklist

- [ ] Which business areas use the affected screens or APIs? Have they been told about the change window?
- [ ] Does the change alter any stored procedure signature used by another system?
- [ ] Has the XML export format changed? If so, has the external vendor been told?
- [ ] Were the tests run (`dotnet test`) and do they pass?
- [ ] Has the support knowledge base been updated?
