using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using Microsoft.Maui.ApplicationModel;

namespace NutritionTracker.Services;

public class AndroidVoiceInputService : IVoiceInputService
{
    public async Task<string?> ListenOnceAsync(CancellationToken cancellationToken = default)
    {
        var micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
        if (micStatus != PermissionStatus.Granted)
            return null;

        var activity = Platform.CurrentActivity;
        if (activity == null || !SpeechRecognizer.IsRecognitionAvailable(activity))
            return null;

        var recognizer = SpeechRecognizer.CreateSpeechRecognizer(activity);
        if (recognizer == null)
            return null;

        var tcs = new TaskCompletionSource<string?>();

        using var _ = cancellationToken.Register(() =>
        {
            if (!tcs.Task.IsCompleted)
                tcs.TrySetCanceled(cancellationToken);
        });

        var listener = new SpeechListener(
            onResult: text =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(text);
            },
            onError: () =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            });

        recognizer.SetRecognitionListener(listener);

        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, System.Globalization.CultureInfo.CurrentUICulture.Name);
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);

        await MainThread.InvokeOnMainThreadAsync(() => recognizer.StartListening(intent));

        try
        {
            var text = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);
            return text;
        }
        catch
        {
            return null;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try { recognizer.StopListening(); } catch { }
                try { recognizer.Cancel(); } catch { }
                recognizer.Destroy();
            });
        }
    }

    private sealed class SpeechListener : Java.Lang.Object, IRecognitionListener
    {
        private readonly Action<string?> _onResult;
        private readonly Action _onError;

        public SpeechListener(Action<string?> onResult, Action onError)
        {
            _onResult = onResult;
            _onError = onError;
        }

        public void OnBeginningOfSpeech() { }

        public void OnBufferReceived(byte[]? buffer) { }

        public void OnEndOfSpeech() { }

        public void OnError([GeneratedEnum] SpeechRecognizerError error) => _onError();

        public void OnEvent(int eventType, Bundle? @params) { }

        public void OnPartialResults(Bundle? partialResults) { }

        public void OnReadyForSpeech(Bundle? @params) { }

        public void OnResults(Bundle? results)
        {
            var list = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            var first = list?.FirstOrDefault();
            _onResult(first);
        }

        public void OnRmsChanged(float rmsdB) { }
    }
}
