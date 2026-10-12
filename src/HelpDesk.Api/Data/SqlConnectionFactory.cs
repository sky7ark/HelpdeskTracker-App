using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace HelpDesk.Api.Data;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}

/// <summary>Creates SQL Server connections from the "HelpDesk" connection string in appsettings.json.</summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HelpDesk")
            ?? throw new InvalidOperationException("Connection string 'HelpDesk' is missing from appsettings.json.");
    }

    public DbConnection CreateConnection() => new SqlConnection(_connectionString);
}
