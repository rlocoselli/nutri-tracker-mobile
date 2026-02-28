namespace NutritionTracker.Services;

public interface IMealReminderService
{
    Task ScheduleDailyMealRemindersAsync(bool enabled, TimeSpan breakfastTime, TimeSpan lunchTime, TimeSpan dinnerTime);
}
