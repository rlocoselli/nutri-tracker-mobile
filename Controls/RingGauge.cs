using Microsoft.Maui.Graphics;

namespace NutritionTracker.Controls;

/// <summary>
/// Simple circular progress ring (0..1) drawn with GraphicsView.
/// </summary>
public class RingGauge : GraphicsView
{
    private readonly RingDrawable _drawable;

    public RingGauge()
    {
        _drawable = new RingDrawable(() => Progress, () => RingColor, () => TrackColor);
        Drawable = _drawable;
        HeightRequest = 96;
        WidthRequest = 96;
    }

    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(RingGauge), 0d,
        propertyChanged: (b, o, n) => ((RingGauge)b).Invalidate());

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(RingGauge), Colors.Teal,
        propertyChanged: (b, o, n) => ((RingGauge)b).Invalidate());

    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(RingGauge), Color.FromArgb("#E6EAF2"),
        propertyChanged: (b, o, n) => ((RingGauge)b).Invalidate());

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    private sealed class RingDrawable : IDrawable
    {
        private readonly Func<double> _progress;
        private readonly Func<Color> _ring;
        private readonly Func<Color> _track;

        public RingDrawable(Func<double> progress, Func<Color> ring, Func<Color> track)
        {
            _progress = progress;
            _ring = ring;
            _track = track;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var p = Math.Clamp(_progress(), 0d, 1d);

            var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var stroke = Math.Max(10f, size * 0.12f);
            var inset = stroke / 2f + 2f;
            var rect = new RectF(dirtyRect.X + inset, dirtyRect.Y + inset, size - inset * 2f, size - inset * 2f);

            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;

            // Track
            canvas.StrokeColor = _track();
            canvas.DrawArc(rect, 0, 360, false, false);

            // Progress (start at -90°)
            if (p > 0)
            {
                canvas.StrokeColor = _ring();
                var sweep = (float)(p * 360d);
                canvas.DrawArc(rect, -90, sweep, false, false);
            }
        }
    }
}
