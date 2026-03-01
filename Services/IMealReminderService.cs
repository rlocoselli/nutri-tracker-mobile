namespace NutritionTracker.Services;

public interface IMealReminderService
{
    Task<bool> ScheduleDailyMealRemindersAsync(bool enabled, TimeSpan breakfastTime, TimeSpan lunchTime, TimeSpan dinnerTime);
}
