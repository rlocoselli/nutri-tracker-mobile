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
        await _db.CreateTableAsync<UserGoals>();

        var goals = await _db.Table<UserGoals>().FirstOrDefaultAsync();
        if (goals == null)
            await _db.InsertAsync(new UserGoals());
    }

    public Task<UserGoals> GetGoalsAsync() => _db.Table<UserGoals>().FirstAsync();
    public Task<int> SaveGoalsAsync(UserGoals g) => _db.InsertOrReplaceAsync(g);

    public async Task SaveMealAsync(MealEntry entry, List<MealItem> items)
    {
        await _db.InsertOrReplaceAsync(entry);
        foreach (var it in items)
            await _db.InsertOrReplaceAsync(it);
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

    public Task<List<MealEntry>> GetMealsBetweenUtcAsync(DateTime fromUtc, DateTime toUtc)
    {
        return _db.Table<MealEntry>()
            .Where(m => m.DateUtc >= fromUtc && m.DateUtc < toUtc)
            .OrderBy(m => m.DateUtc)
            .ToListAsync();
    }
}
