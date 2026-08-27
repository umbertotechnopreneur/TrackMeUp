using Microsoft.Data.Sqlite;

namespace TrackMeUp.Services;

/// <summary>Owns the activity-store connection contract and applies required PRAGMAs consistently.</summary>
internal sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    internal SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            // Profiling shows a faster open path with pooling, but LocalStore does not yet own a
            // deterministic disposal boundary for every import/reset/test caller. Keep the safe
            // lifecycle contract until pool clearing can be guaranteed before file replacement.
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
