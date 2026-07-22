using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StudyLiveAssistant.Core;
using CountdownModel = StudyLiveAssistant.Core.CountdownEvent;

namespace StudyLiveAssistant.App.Infrastructure;

public sealed class LocalDatabase : ITaskRepository, ISettingsRepository, IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    public LocalDatabase(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(Path.Combine(dataDirectory, "Assets"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "Logs"));
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "data.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA foreign_keys=ON;
                CREATE TABLE IF NOT EXISTS schema_info(version INTEGER NOT NULL);
                INSERT INTO schema_info(version) SELECT 1 WHERE NOT EXISTS(SELECT 1 FROM schema_info);
                CREATE TABLE IF NOT EXISTS categories(
                    id TEXT PRIMARY KEY, parent_id TEXT NULL, name TEXT NOT NULL,
                    accent_color TEXT NOT NULL, sort_order INTEGER NOT NULL, is_built_in INTEGER NOT NULL,
                    FOREIGN KEY(parent_id) REFERENCES categories(id) ON DELETE RESTRICT);
                CREATE TABLE IF NOT EXISTS study_tasks(
                    id TEXT PRIMARY KEY, task_date TEXT NOT NULL, primary_category_id TEXT NOT NULL,
                    secondary_category_id TEXT NOT NULL, detail TEXT NOT NULL, scheduled_start TEXT NOT NULL,
                    progress_kind INTEGER NOT NULL, unit INTEGER NOT NULL, custom_unit TEXT NOT NULL,
                    target_value REAL NOT NULL, current_value REAL NOT NULL, adjustment_step REAL NOT NULL,
                    elapsed_seconds INTEGER NOT NULL, expected_total_minutes INTEGER NULL, status INTEGER NOT NULL,
                    creation_order INTEGER NOT NULL,
                    FOREIGN KEY(primary_category_id) REFERENCES categories(id) ON DELETE RESTRICT,
                    FOREIGN KEY(secondary_category_id) REFERENCES categories(id) ON DELETE RESTRICT);
                CREATE INDEX IF NOT EXISTS idx_tasks_date_start ON study_tasks(task_date, scheduled_start, creation_order);
                CREATE TABLE IF NOT EXISTS daily_plans(task_date TEXT PRIMARY KEY, target_minutes INTEGER NULL);
                CREATE TABLE IF NOT EXISTS study_sessions(
                    id TEXT PRIMARY KEY, task_id TEXT NOT NULL, started_at TEXT NOT NULL, ended_at TEXT NOT NULL,
                    duration_seconds INTEGER NOT NULL, end_reason TEXT NOT NULL,
                    FOREIGN KEY(task_id) REFERENCES study_tasks(id) ON DELETE CASCADE);
                CREATE INDEX IF NOT EXISTS idx_sessions_started ON study_sessions(started_at);
                CREATE TABLE IF NOT EXISTS countdown_events(
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, target_date TEXT NOT NULL,
                    is_enabled INTEGER NOT NULL, sort_order INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS app_settings(id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await SeedCategoriesAsync(connection, cancellationToken);
            await SeedCountdownAsync(connection, cancellationToken);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<TaskCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync<IReadOnlyList<TaskCategory>>(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT id,parent_id,name,accent_color,sort_order,is_built_in FROM categories ORDER BY parent_id IS NOT NULL,sort_order,name";
            var result = new List<TaskCategory>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new TaskCategory
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    ParentId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
                    Name = reader.GetString(2), AccentColor = reader.GetString(3),
                    SortOrder = reader.GetInt32(4), IsBuiltIn = reader.GetInt32(5) != 0
                });
            }
            return result;
        }, cancellationToken);

    public Task SaveCategoryAsync(TaskCategory category, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO categories(id,parent_id,name,accent_color,sort_order,is_built_in)
                VALUES($id,$parent,$name,$color,$sort,$built)
                ON CONFLICT(id) DO UPDATE SET parent_id=$parent,name=$name,accent_color=$color,sort_order=$sort;
                """;
            command.Parameters.AddWithValue("$id", category.Id.ToString());
            command.Parameters.AddWithValue("$parent", (object?)category.ParentId?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", category.Name.Trim());
            command.Parameters.AddWithValue("$color", category.AccentColor);
            command.Parameters.AddWithValue("$sort", category.SortOrder);
            command.Parameters.AddWithValue("$built", category.IsBuiltIn ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var used = connection.CreateCommand();
            used.Transaction = (SqliteTransaction)transaction;
            used.CommandText = """
                SELECT COUNT(*) FROM study_tasks
                WHERE primary_category_id=$id OR secondary_category_id=$id
                   OR secondary_category_id IN (SELECT id FROM categories WHERE parent_id=$id);
                """;
            used.Parameters.AddWithValue("$id", id.ToString());
            if (Convert.ToInt32(await used.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("该类型或其子类型已被任务使用，不能删除。");
            var children = connection.CreateCommand();
            children.Transaction = (SqliteTransaction)transaction;
            children.CommandText = "DELETE FROM categories WHERE parent_id=$id";
            children.Parameters.AddWithValue("$id", id.ToString());
            await children.ExecuteNonQueryAsync(cancellationToken);
            var category = connection.CreateCommand();
            category.Transaction = (SqliteTransaction)transaction;
            category.CommandText = "DELETE FROM categories WHERE id=$id";
            category.Parameters.AddWithValue("$id", id.ToString());
            await category.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<StudyTask>> GetTasksAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        WithConnectionAsync<IReadOnlyList<StudyTask>>(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,task_date,primary_category_id,secondary_category_id,detail,scheduled_start,
                       progress_kind,unit,custom_unit,target_value,current_value,adjustment_step,
                       elapsed_seconds,expected_total_minutes,status,creation_order
                FROM study_tasks WHERE task_date=$date ORDER BY scheduled_start,creation_order,id;
                """;
            command.Parameters.AddWithValue("$date", Format(date));
            var result = new List<StudyTask>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(ReadTask(reader));
            return result;
        }, cancellationToken);

    public Task SaveTaskAsync(StudyTask task, CancellationToken cancellationToken = default)
    {
        TaskRules.Validate(task);
        return WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO study_tasks(id,task_date,primary_category_id,secondary_category_id,detail,scheduled_start,
                    progress_kind,unit,custom_unit,target_value,current_value,adjustment_step,elapsed_seconds,
                    expected_total_minutes,status,creation_order)
                VALUES($id,$date,$primary,$secondary,$detail,$start,$kind,$unit,$custom,$target,$current,$step,$elapsed,$expected,$status,$created)
                ON CONFLICT(id) DO UPDATE SET task_date=$date,primary_category_id=$primary,secondary_category_id=$secondary,
                    detail=$detail,scheduled_start=$start,progress_kind=$kind,unit=$unit,custom_unit=$custom,
                    target_value=$target,current_value=$current,adjustment_step=$step,elapsed_seconds=$elapsed,
                    expected_total_minutes=$expected,status=$status;
                """;
            AddTaskParameters(command, task);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM study_tasks WHERE id=$id";
            command.Parameters.AddWithValue("$id", id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public async Task CopyTasksAsync(DateOnly source, DateOnly destination, CancellationToken cancellationToken = default)
    {
        var tasks = await GetTasksAsync(source, cancellationToken);
        var order = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var sourceTask in tasks)
        {
            var copy = new StudyTask
            {
                Id = Guid.NewGuid(), Date = destination,
                PrimaryCategoryId = sourceTask.PrimaryCategoryId, SecondaryCategoryId = sourceTask.SecondaryCategoryId,
                Detail = sourceTask.Detail, ScheduledStart = sourceTask.ScheduledStart,
                ProgressKind = sourceTask.ProgressKind, Unit = sourceTask.Unit, CustomUnit = sourceTask.CustomUnit,
                TargetValue = sourceTask.TargetValue, AdjustmentStep = sourceTask.AdjustmentStep,
                ExpectedTotalMinutes = sourceTask.ExpectedTotalMinutes, CreationOrder = order++
            };
            await SaveTaskAsync(copy, cancellationToken);
        }
    }

    public Task<DailyPlan?> GetDailyPlanAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        WithConnectionAsync<DailyPlan?>(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT target_minutes FROM daily_plans WHERE task_date=$date";
            command.Parameters.AddWithValue("$date", Format(date));
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null ? null : new DailyPlan { Date = date, TargetStudyMinutes = value is DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture) };
        }, cancellationToken);

    public Task SaveDailyPlanAsync(DailyPlan plan, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO daily_plans(task_date,target_minutes) VALUES($date,$target) ON CONFLICT(task_date) DO UPDATE SET target_minutes=$target";
            command.Parameters.AddWithValue("$date", Format(plan.Date));
            command.Parameters.AddWithValue("$target", (object?)plan.TargetStudyMinutes ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task SaveSessionAsync(StudySession session, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO study_sessions(id,task_id,started_at,ended_at,duration_seconds,end_reason)
                VALUES($id,$task,$start,$end,$duration,$reason)
                ON CONFLICT(id) DO UPDATE SET ended_at=$end,duration_seconds=$duration,end_reason=$reason;
                """;
            command.Parameters.AddWithValue("$id", session.Id.ToString());
            command.Parameters.AddWithValue("$task", session.TaskId.ToString());
            command.Parameters.AddWithValue("$start", session.StartedAt.ToString("O"));
            command.Parameters.AddWithValue("$end", session.EndedAt.ToString("O"));
            command.Parameters.AddWithValue("$duration", session.DurationSeconds);
            command.Parameters.AddWithValue("$reason", session.EndReason);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<StudySession>> GetSessionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        WithConnectionAsync<IReadOnlyList<StudySession>>(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT id,task_id,started_at,ended_at,duration_seconds,end_reason FROM study_sessions ORDER BY started_at";
            var result = new List<StudySession>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = new StudySession
                {
                    Id = Guid.Parse(reader.GetString(0)), TaskId = Guid.Parse(reader.GetString(1)),
                    StartedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                    EndedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    DurationSeconds = reader.GetInt64(4), EndReason = reader.GetString(5)
                };
                var localDate = DateOnly.FromDateTime(item.StartedAt.LocalDateTime);
                if (localDate >= from && localDate <= to) result.Add(item);
            }
            return result;
        }, cancellationToken);

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT json FROM app_settings WHERE id=1";
            var json = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
            try { return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings(); }
            catch (JsonException) { return new AppSettings(); }
        }, cancellationToken);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO app_settings(id,json) VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET json=$json";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(settings, _jsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task<IReadOnlyList<CountdownModel>> GetCountdownEventsAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync<IReadOnlyList<CountdownModel>>(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT id,name,target_date,is_enabled,sort_order FROM countdown_events ORDER BY sort_order,name";
            var result = new List<CountdownModel>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(new CountdownModel { Id = Guid.Parse(reader.GetString(0)), Name = reader.GetString(1), TargetDate = DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture), IsEnabled = reader.GetInt32(3) != 0, SortOrder = reader.GetInt32(4) });
            return result;
        }, cancellationToken);

    public Task SaveCountdownEventAsync(CountdownModel countdown, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO countdown_events(id,name,target_date,is_enabled,sort_order) VALUES($id,$name,$date,$enabled,$sort) ON CONFLICT(id) DO UPDATE SET name=$name,target_date=$date,is_enabled=$enabled,sort_order=$sort";
            command.Parameters.AddWithValue("$id", countdown.Id.ToString());
            command.Parameters.AddWithValue("$name", countdown.Name.Trim());
            command.Parameters.AddWithValue("$date", Format(countdown.TargetDate));
            command.Parameters.AddWithValue("$enabled", countdown.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sort", countdown.SortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task DeleteCountdownEventAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(async connection =>
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM countdown_events WHERE id=$id";
            command.Parameters.AddWithValue("$id", id.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _gate.Dispose();
    }

    private async Task WithConnectionAsync(Func<SqliteConnection, Task> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await action(connection);
        }
        finally { _gate.Release(); }
    }

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return await action(connection);
        }
        finally { _gate.Release(); }
    }

    private static StudyTask ReadTask(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)), Date = DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        PrimaryCategoryId = Guid.Parse(reader.GetString(2)), SecondaryCategoryId = Guid.Parse(reader.GetString(3)),
        Detail = reader.GetString(4), ScheduledStart = TimeOnly.ParseExact(reader.GetString(5), "HH:mm", CultureInfo.InvariantCulture),
        ProgressKind = (ProgressKind)reader.GetInt32(6), Unit = (ProgressUnit)reader.GetInt32(7), CustomUnit = reader.GetString(8),
        TargetValue = reader.GetDouble(9), CurrentValue = reader.GetDouble(10), AdjustmentStep = reader.GetDouble(11),
        ElapsedSeconds = reader.GetInt64(12), ExpectedTotalMinutes = reader.IsDBNull(13) ? null : reader.GetInt32(13),
        Status = (StudyTaskStatus)reader.GetInt32(14), CreationOrder = reader.GetInt64(15)
    };

    private static void AddTaskParameters(SqliteCommand command, StudyTask task)
    {
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$date", Format(task.Date));
        command.Parameters.AddWithValue("$primary", task.PrimaryCategoryId.ToString());
        command.Parameters.AddWithValue("$secondary", task.SecondaryCategoryId.ToString());
        command.Parameters.AddWithValue("$detail", task.Detail.Trim());
        command.Parameters.AddWithValue("$start", task.ScheduledStart.ToString("HH:mm", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$kind", (int)task.ProgressKind);
        command.Parameters.AddWithValue("$unit", (int)task.Unit);
        command.Parameters.AddWithValue("$custom", task.CustomUnit.Trim());
        command.Parameters.AddWithValue("$target", task.TargetValue);
        command.Parameters.AddWithValue("$current", task.CurrentValue);
        command.Parameters.AddWithValue("$step", task.AdjustmentStep);
        command.Parameters.AddWithValue("$elapsed", task.ElapsedSeconds);
        command.Parameters.AddWithValue("$expected", (object?)task.ExpectedTotalMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$created", task.CreationOrder);
    }

    private static async Task SeedCategoriesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM categories";
        if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0) return;

        var seeds = new (string Name, string Color, string[] Children)[]
        {
            ("数学", "#6F9FD8", ["听课", "做题", "模拟卷", "真题卷"]),
            ("专业课", "#6EAD8F", ["听课", "做题", "实验", "复习"]),
            ("英语", "#A487C4", ["单词", "阅读", "听力", "真题"]),
            ("政治", "#D19372", ["听课", "刷题", "背诵", "真题"]),
            ("吃饭", "#D4A85C", ["早餐", "午餐", "晚餐"]),
            ("娱乐", "#7EA8A8", ["游戏", "视频", "放松"]),
            ("午休", "#8F9BB3", ["午休"])
        };
        var order = 0;
        foreach (var seed in seeds)
        {
            var parent = Guid.NewGuid();
            await InsertCategoryAsync(connection, parent, null, seed.Name, seed.Color, order++, cancellationToken);
            var childOrder = 0;
            foreach (var child in seed.Children)
                await InsertCategoryAsync(connection, Guid.NewGuid(), parent, child, seed.Color, childOrder++, cancellationToken);
        }
    }

    private static async Task InsertCategoryAsync(SqliteConnection connection, Guid id, Guid? parent, string name, string color, int order, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO categories(id,parent_id,name,accent_color,sort_order,is_built_in) VALUES($id,$parent,$name,$color,$sort,1)";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$parent", (object?)parent?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$color", color);
        command.Parameters.AddWithValue("$sort", order);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SeedCountdownAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO countdown_events(id,name,target_date,is_enabled,sort_order) SELECT $id,'目标日',$date,1,0 WHERE NOT EXISTS(SELECT 1 FROM countdown_events)";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$date", Format(DateOnly.FromDateTime(DateTime.Today.AddDays(100))));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
