namespace NutritionTracker.Services;

public interface IVoiceInputService
{
    Task<string?> ListenOnceAsync(CancellationToken cancellationToken = default);
}
