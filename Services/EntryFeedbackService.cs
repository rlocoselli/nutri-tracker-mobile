namespace NutritionTracker.Services;

public sealed class EntryFeedbackService : IEntryFeedbackService
{
    public Task PlayEntryAddedAsync()
    {
#if ANDROID
        try
        {
            using var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 90);
            tone.StartTone(Android.Media.Tone.PropAck, 170);
        }
        catch
        {
            // Optional UX feedback only.
        }
#endif
        return Task.CompletedTask;
    }
}
