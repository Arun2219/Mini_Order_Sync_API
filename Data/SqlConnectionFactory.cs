using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Mini_Order_Sync_API.Data;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string 'DefaultConnection' was not found.");
    }

    public IDbConnection CreateConnection()
    {
        if (_connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) ||
            _connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) && !_connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            return new SqliteConnection(_connectionString);
        }

        return new SqlConnection(_connectionString);
    }
}

