using Microsoft.Data.Sqlite;
using StudyLiveAssistant.App.Infrastructure;

namespace StudyLiveAssistant.Tests;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task Initialize_AddsActualStartColumnToVersionOneDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), "StudyLiveAssistantMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "data.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE schema_info(version INTEGER NOT NULL);
                    INSERT INTO schema_info(version) VALUES(1);
                    CREATE TABLE study_tasks(
                        id TEXT PRIMARY KEY, task_date TEXT NOT NULL, primary_category_id TEXT NOT NULL,
                        secondary_category_id TEXT NOT NULL, detail TEXT NOT NULL, scheduled_start TEXT NOT NULL,
                        progress_kind INTEGER NOT NULL, unit INTEGER NOT NULL, custom_unit TEXT NOT NULL,
                        target_value REAL NOT NULL, current_value REAL NOT NULL, adjustment_step REAL NOT NULL,
                        elapsed_seconds INTEGER NOT NULL, expected_total_minutes INTEGER NULL, status INTEGER NOT NULL,
                        creation_order INTEGER NOT NULL);
                    """;
                await command.ExecuteNonQueryAsync();
            }

            using var database = new LocalDatabase(directory);
            await database.InitializeAsync();

            await using var verification = new SqliteConnection($"Data Source={path}");
            await verification.OpenAsync();
            var info = verification.CreateCommand();
            info.CommandText = "SELECT COUNT(*) FROM pragma_table_info('study_tasks') WHERE name='actual_started_at'";
            Assert.Equal(1L, (long)(await info.ExecuteScalarAsync())!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
