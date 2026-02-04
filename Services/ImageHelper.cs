namespace NutritionTracker.Services;

public static class ImageHelper
{
    public static async Task<(byte[] bytes, string mime, string localPath)?> PickOrCaptureJpegAsync(bool capture)
    {
        FileResult? photo = capture
            ? await MediaPicker.Default.CapturePhotoAsync()
            : await MediaPicker.Default.PickPhotoAsync();

        if (photo == null) return null;

        // Save a local copy (so we can show preview later)
        var localPath = Path.Combine(FileSystem.AppDataDirectory, $"meal_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg");
        await using (var src = await photo.OpenReadAsync())
        await using (var dst = File.OpenWrite(localPath))
        {
            await src.CopyToAsync(dst);
        }

        var bytes = await File.ReadAllBytesAsync(localPath);
        return (bytes, "image/jpeg", localPath);
    }
}
