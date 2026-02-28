using NutritionTracker.Models;

namespace NutritionTracker.Services;

public static class DailyRewardService
{
    public static bool IsBalancedDay(UserGoals goals, double caloriesIn, double carbsIn, double proteinIn, double burnedCalories)
    {
        if (caloriesIn <= 0 && carbsIn <= 0 && proteinIn <= 0)
            return false;

        var calorieTarget = Math.Max(1, goals.CaloriesTarget);
        var proteinTarget = Math.Max(1, goals.ProteinGTarget);
        var carbsTarget = Math.Max(1, goals.CarbsGTarget);

        var netCalories = caloriesIn - Math.Max(0, burnedCalories);

        var caloriesOk = RelativeDelta(netCalories, calorieTarget) <= 0.15;
        var proteinOk = proteinIn >= proteinTarget * 0.9;
        var carbsOk = carbsIn >= carbsTarget * 0.8 && carbsIn <= carbsTarget * 1.2;

        var score = 0;
        if (caloriesOk) score++;
        if (proteinOk) score++;
        if (carbsOk) score++;

        return score >= 2;
    }

    public static int ComputeCurrentStreak(UserGoals goals, Func<DateTime, Task<(double caloriesIn, double carbsIn, double proteinIn, double burnedCalories)>> dayProvider, int maxDays = 30)
    {
        return ComputeCurrentStreakAsync(goals, dayProvider, maxDays).GetAwaiter().GetResult();
    }

    public static async Task<int> ComputeCurrentStreakAsync(UserGoals goals, Func<DateTime, Task<(double caloriesIn, double carbsIn, double proteinIn, double burnedCalories)>> dayProvider, int maxDays = 30)
    {
        var streak = 0;

        for (var i = 0; i < maxDays; i++)
        {
            var dayLocal = DateTime.Now.Date.AddDays(-i);
            var day = await dayProvider(dayLocal);
            if (!IsBalancedDay(goals, day.caloriesIn, day.carbsIn, day.proteinIn, day.burnedCalories))
                break;
            streak++;
        }

        return streak;
    }

    private static double RelativeDelta(double value, double target)
    {
        if (target <= 0) return 1;
        return Math.Abs(value - target) / target;
    }
}
