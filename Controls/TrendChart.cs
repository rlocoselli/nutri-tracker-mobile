using Microsoft.Maui.Graphics;

namespace NutritionTracker.Controls;

/// <summary>
/// Simple trend chart (line) using GraphicsView.
/// Supports binding to Values/Labels and auto refresh on changes.
/// </summary>
public class TrendChart : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values),
        typeof(IList<double>),
        typeof(TrendChart),
        defaultValue: Array.Empty<double>(),
        propertyChanged: (b, o, n) => ((TrendChart)b).Invalidate());

    public static readonly BindableProperty LabelsProperty = BindableProperty.Create(
        nameof(Labels),
        typeof(IList<string>),
        typeof(TrendChart),
        defaultValue: Array.Empty<string>(),
        propertyChanged: (b, o, n) => ((TrendChart)b).Invalidate());

    public IList<double> Values
    {
        get => (IList<double>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IList<string> Labels
    {
        get => (IList<string>)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public TrendChart()
    {
        Drawable = new TrendChartDrawable(this);
    }

    private sealed class TrendChartDrawable : IDrawable
    {
        private readonly TrendChart _chart;

        public TrendChartDrawable(TrendChart chart) => _chart = chart;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = _chart.Values ?? Array.Empty<double>();
            // With 0 points, show empty state. With 1 point, still draw the axis + a dot.
            if (values.Count == 0)
            {
                DrawEmpty(canvas, dirtyRect);
                return;
            }

            // Colors from resources when possible
            var accent = TryGetColor("Accent") ?? Colors.DodgerBlue;
            var muted = TryGetColor("Muted") ?? Colors.Gray;
            var text = TryGetColor("Text") ?? Colors.Black;
            var card = TryGetColor("Card") ?? Colors.White;

            var leftPad = 10f;
            var rightPad = 10f;
            var topPad = 10f;
            var bottomPad = 22f;

            var w = dirtyRect.Width;
            var h = dirtyRect.Height;
            var plot = new RectF(
                dirtyRect.X + leftPad,
                dirtyRect.Y + topPad,
                Math.Max(1, w - leftPad - rightPad),
                Math.Max(1, h - topPad - bottomPad));

            var min = values.Min();
            var max = values.Max();
            if (Math.Abs(max - min) < 0.0001) max = min + 1; // avoid flat division

            // Background card
            canvas.FillColor = card.WithAlpha(0.35f);
            canvas.FillRoundedRectangle(plot, 8f);

            // Horizontal grid lines
            canvas.StrokeColor = muted.WithAlpha(0.35f);
            canvas.StrokeSize = 1;
            var gridSteps = 4;
            for (var i = 0; i <= gridSteps; i++)
            {
                var y = plot.Top + plot.Height * i / gridSteps;
                canvas.DrawLine(plot.Left, y, plot.Right, y);
            }

            // Axis baseline
            canvas.StrokeColor = muted;
            canvas.StrokeSize = 1;
            canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // Line
            canvas.StrokeColor = accent;
            canvas.StrokeSize = 2;
            canvas.Antialias = true;

            var count = values.Count;
            float X(int i) => plot.Left + (count == 1 ? (plot.Width / 2f) : (plot.Width * i / (count - 1f)));
            float Y(double v) => plot.Bottom - (float)((v - min) / (max - min) * plot.Height);

            if (count >= 2)
            {
                var p0 = new PointF(X(0), Y(values[0]));

                // Area fill under line
                var path = new PathF();
                path.MoveTo(p0.X, plot.Bottom);
                path.LineTo(p0.X, p0.Y);

                for (var i = 1; i < count; i++)
                {
                    var p1 = new PointF(X(i), Y(values[i]));
                    canvas.DrawLine(p0, p1);
                    path.LineTo(p1.X, p1.Y);
                    p0 = p1;
                }

                path.LineTo(X(count - 1), plot.Bottom);
                path.Close();
                canvas.FillColor = accent.WithAlpha(0.15f);
                canvas.FillPath(path);
            }

            // Points
            canvas.FillColor = accent;
            for (var i = 0; i < count; i++)
            {
                var px = X(i);
                var py = Y(values[i]);
                canvas.FillCircle(px, py, 2.8f);
            }

            // Highlight latest point and value
            var lastX = X(count - 1);
            var lastY = Y(values[count - 1]);
            canvas.FillColor = accent;
            canvas.FillCircle(lastX, lastY, 4.2f);
            canvas.FontColor = text;
            canvas.FontSize = 11;
            canvas.DrawString($"{Math.Round(values[count - 1])}", lastX + 6, Math.Max(plot.Top, lastY - 14), HorizontalAlignment.Left);

            // Labels (first / middle / last)
            var labels = _chart.Labels ?? Array.Empty<string>();
            if (labels.Count == count)
            {
                canvas.FontColor = text;
                canvas.FontSize = 11;
                if (count == 1)
                {
                    DrawLabel(canvas, plot, labels[0], 0, X);
                }
                else
                {
                    DrawLabel(canvas, plot, labels[0], 0, X);
                    if (count > 2) DrawLabel(canvas, plot, labels[count / 2], count / 2, X);
                    DrawLabel(canvas, plot, labels[count - 1], count - 1, X);
                }
            }
        }

        private static void DrawEmpty(ICanvas canvas, RectF rect)
        {
            var muted = TryGetColor("Muted") ?? Colors.Gray;
            canvas.FontColor = muted;
            canvas.FontSize = 12;
            var lang = Preferences.Default.Get("app_lang", "fr");
            var label = lang == "en" ? "No data" : "Pas de données";
            canvas.DrawString(label, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        private static void DrawLabel(ICanvas canvas, RectF plot, string text, int index, Func<int, float> x)
        {
            canvas.DrawString(text, x(index), plot.Bottom + 4, HorizontalAlignment.Center);
        }

        private static Color? TryGetColor(string key)
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var obj) == true && obj is Color c)
                return c;
            return null;
        }
    }
}
