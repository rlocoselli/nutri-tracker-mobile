namespace NutritionTracker.Services;

public class PointsService
{
    private const string BalanceKey = "app_points_balance";

    public int GetBalance() => Preferences.Default.Get(BalanceKey, 0);

    public int Award(int points)
    {
        var safePoints = Math.Max(0, points);
        var current = GetBalance();
        var next = current + safePoints;
        Preferences.Default.Set(BalanceKey, next);
        return next;
    }
}
