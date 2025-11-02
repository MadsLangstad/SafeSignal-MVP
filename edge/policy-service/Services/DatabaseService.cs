using Microsoft.Data.Sqlite;
using Dapper;

namespace SafeSignal.Edge.PolicyService.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        var dbPath = configuration["Database:Path"] ?? "/data/safesignal.db";
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing SQLite database...");

        using var connection = GetConnection();
        await connection.OpenAsync();

        // Read and execute schema
        var schemaPath = "/app/schema/init-schema.sql";
        if (File.Exists(schemaPath))
        {
            var schema = await File.ReadAllTextAsync(schemaPath);
            await connection.ExecuteAsync(schema);
            _logger.LogInformation("Database schema initialized successfully");
        }
        else
        {
            _logger.LogWarning("Schema file not found at {Path}, skipping initialization", schemaPath);
        }

        // Verify tables exist
        var tableCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table'");
        _logger.LogInformation("Database initialized with {TableCount} tables", tableCount);
    }

    public async Task<Dictionary<string, int>> GetStatisticsAsync()
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        var stats = new Dictionary<string, int>
        {
            ["total_alerts"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM alerts"),
            ["total_buildings"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM buildings"),
            ["total_rooms"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM rooms"),
            ["total_devices"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM devices"),
            ["active_devices"] = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM devices WHERE status = 'ACTIVE'"),
            ["alerts_today"] = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM alerts WHERE DATE(created_at) = DATE('now')")
        };

        return stats;
    }
}
