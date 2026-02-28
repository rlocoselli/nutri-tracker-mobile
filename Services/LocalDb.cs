using SQLite;
using NutritionTracker.Models;

namespace NutritionTracker.Services;

public class LocalDb
{
    private readonly SQLiteAsyncConnection _db;

    public LocalDb(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitAsync()
    {
        await _db.CreateTableAsync<MealEntry>();
        await _db.CreateTableAsync<MealItem>();
        await _db.CreateTableAsync<ExerciseEntry>();
        await _db.CreateTableAsync<UserGoals>();
        await EnsureMealEntryColumnsAsync();

        var goals = await _db.Table<UserGoals>().FirstOrDefaultAsync();
        if (goals == null)
            await _db.InsertAsync(new UserGoals());
    }

    private async Task EnsureMealEntryColumnsAsync()
    {
        var columns = await _db.QueryAsync<TableInfoRow>("PRAGMA table_info('MealEntry')");
        var hasDescription = columns.Any(c => string.Equals(c.Name, "Description", StringComparison.OrdinalIgnoreCase));
        if (!hasDescription)
            await _db.ExecuteAsync("ALTER TABLE MealEntry ADD COLUMN Description TEXT NOT NULL DEFAULT ''");

        var hasAiNotes = columns.Any(c => string.Equals(c.Name, "AiNotes", StringComparison.OrdinalIgnoreCase));
        if (!hasAiNotes)
            await _db.ExecuteAsync("ALTER TABLE MealEntry ADD COLUMN AiNotes TEXT NOT NULL DEFAULT ''");

        var hasQualityScore = columns.Any(c => string.Equals(c.Name, "QualityScore", StringComparison.OrdinalIgnoreCase));
        if (!hasQualityScore)
            await _db.ExecuteAsync("ALTER TABLE MealEntry ADD COLUMN QualityScore REAL NOT NULL DEFAULT 0");

        var hasQualityLabel = columns.Any(c => string.Equals(c.Name, "QualityLabel", StringComparison.OrdinalIgnoreCase));
        if (!hasQualityLabel)
            await _db.ExecuteAsync("ALTER TABLE MealEntry ADD COLUMN QualityLabel TEXT NOT NULL DEFAULT ''");
    }

    private sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; set; } = "";
    }

    public Task<UserGoals> GetGoalsAsync() => _db.Table<UserGoals>().FirstAsync();
    public Task<int> SaveGoalsAsync(UserGoals g) => _db.InsertOrReplaceAsync(g);

    public async Task SaveMealAsync(MealEntry entry, List<MealItem> items)
    {
        await _db.InsertOrReplaceAsync(entry);
        foreach (var it in items)
            await _db.InsertOrReplaceAsync(it);
    }

    public Task<int> UpsertMealEntryAsync(MealEntry entry)
    {
        return _db.InsertOrReplaceAsync(entry);
    }

    public async Task<int> DeleteMealAsync(string mealEntryId)
    {
        var linkedItems = await _db.Table<MealItem>()
            .Where(x => x.MealEntryId == mealEntryId)
            .ToListAsync();

        foreach (var item in linkedItems)
            await _db.DeleteAsync(item);

        return await _db.DeleteAsync<MealEntry>(mealEntryId);
    }

    public async Task<(double cal, double carbs, double prot)> GetTotalsForDayUtcAsync(DateTime dayUtc)
    {
        var key = dayUtc.ToString("yyyy-MM-dd");
        var meals = await _db.Table<MealEntry>().Where(m => m.DayKeyUtc == key).ToListAsync();
        return (meals.Sum(m => m.TotalCalories), meals.Sum(m => m.TotalCarbsG), meals.Sum(m => m.TotalProteinG));
    }

    public Task<List<MealEntry>> GetMealsLastDaysAsync(int days)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days + 1);
        return _db.Table<MealEntry>().Where(m => m.DateUtc >= from).OrderBy(m => m.DateUtc).ToListAsync();
    }

    public Task<List<ExerciseEntry>> GetExercisesLastDaysAsync(int days)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days + 1);
        return _db.Table<ExerciseEntry>().Where(x => x.DateUtc >= from).OrderBy(x => x.DateUtc).ToListAsync();
    }

    public Task<List<MealEntry>> GetMealsBetweenUtcAsync(DateTime fromUtc, DateTime toUtc)
    {
        return _db.Table<MealEntry>()
            .Where(m => m.DateUtc >= fromUtc && m.DateUtc < toUtc)
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }

    public Task<int> SaveExerciseAsync(ExerciseEntry entry)
    {
        return _db.InsertOrReplaceAsync(entry);
    }

    public Task<List<MealItem>> GetMealItemsForEntryAsync(string mealEntryId)
    {
        return _db.Table<MealItem>()
            .Where(x => x.MealEntryId == mealEntryId)
            .ToListAsync();
    }

    public async Task UpsertGoogleFitDailyAsync(DateTime dayLocal, int steps, double burnedCalories)
    {
        var dateUtc = DateTime.SpecifyKind(dayLocal, DateTimeKind.Local).ToUniversalTime();
        var dayKeyUtc = dateUtc.ToString("yyyy-MM-dd");

        var existing = await _db.Table<ExerciseEntry>()
            .Where(x => x.DayKeyUtc == dayKeyUtc && x.Source == "google-fit-sync")
            .ToListAsync();

        foreach (var row in existing)
            await _db.DeleteAsync(row);

        var fresh = new ExerciseEntry
        {
            DateUtc = dateUtc,
            DayKeyUtc = dayKeyUtc,
            GoogleFitSteps = Math.Max(0, steps),
            BurnedCalories = Math.Max(0, burnedCalories),
            ExerciseMinutes = 0,
            Source = "google-fit-sync",
            Notes = "Google Fit"
        };

        await _db.InsertAsync(fresh);
    }

    public async Task<(double burnedCalories, int steps, double minutes)> GetExerciseTotalsBetweenUtcAsync(DateTime fromUtc, DateTime toUtc)
    {
        var entries = await _db.Table<ExerciseEntry>()
            .Where(x => x.DateUtc >= fromUtc && x.DateUtc < toUtc)
            .ToListAsync();

        return (
            entries.Sum(x => x.BurnedCalories),
            entries.Sum(x => x.GoogleFitSteps),
            entries.Sum(x => x.ExerciseMinutes)
        );
    }
}
