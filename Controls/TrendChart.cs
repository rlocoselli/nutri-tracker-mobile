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

            var leftPad = 12f;
            var rightPad = 12f;
            var topPad = 12f;
            var bottomPad = 26f;

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
            canvas.FillColor = card.WithAlpha(0.42f);
            canvas.FillRoundedRectangle(plot, 8f);

            // Horizontal grid lines
            canvas.StrokeColor = muted.WithAlpha(0.26f);
            canvas.StrokeSize = 1;
            var gridSteps = 4;
            for (var i = 0; i <= gridSteps; i++)
            {
                var y = plot.Top + plot.Height * i / gridSteps;
                canvas.DrawLine(plot.Left, y, plot.Right, y);
            }

            // Axis baseline
            canvas.StrokeColor = muted.WithAlpha(0.7f);
            canvas.StrokeSize = 1;
            canvas.DrawLine(plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // Line
            canvas.StrokeColor = accent;
            canvas.StrokeSize = 2.4f;
            canvas.Antialias = true;

            var count = values.Count;
            float X(int i) => plot.Left + (count == 1 ? (plot.Width / 2f) : (plot.Width * i / (count - 1f)));
            float Y(double v) => plot.Bottom - (float)((v - min) / (max - min) * plot.Height);

            if (count >= 2)
            {
                var linePoints = new List<PointF>(count);
                for (var i = 0; i < count; i++)
                    linePoints.Add(new PointF(X(i), Y(values[i])));

                var smooth = BuildSmoothPath(linePoints);
                canvas.DrawPath(smooth);

                // Area fill under line
                var area = BuildSmoothAreaPath(linePoints, plot.Bottom);
                canvas.FillColor = accent.WithAlpha(0.16f);
                canvas.FillPath(area);
            }

            // Points
            canvas.FillColor = accent;
            for (var i = 0; i < count; i++)
            {
                var px = X(i);
                var py = Y(values[i]);
                canvas.FillCircle(px, py, 3.1f);
            }

            // Highlight latest point and value
            var lastX = X(count - 1);
            var lastY = Y(values[count - 1]);
            canvas.FillColor = accent;
            canvas.FillCircle(lastX, lastY, 5.2f);
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

        private static PathF BuildSmoothPath(IReadOnlyList<PointF> points)
        {
            var path = new PathF();
            if (points.Count == 0)
                return path;

            path.MoveTo(points[0].X, points[0].Y);
            if (points.Count == 1)
                return path;

            for (var i = 1; i < points.Count; i++)
            {
                var previous = points[i - 1];
                var current = points[i];
                var midX = (previous.X + current.X) / 2f;
                var midY = (previous.Y + current.Y) / 2f;
                path.QuadTo(previous.X, previous.Y, midX, midY);
            }

            var last = points[^1];
            path.LineTo(last.X, last.Y);
            return path;
        }

        private static PathF BuildSmoothAreaPath(IReadOnlyList<PointF> points, float baselineY)
        {
            var path = new PathF();
            if (points.Count == 0)
                return path;

            path.MoveTo(points[0].X, baselineY);
            path.LineTo(points[0].X, points[0].Y);

            if (points.Count == 1)
            {
                path.LineTo(points[0].X, baselineY);
                path.Close();
                return path;
            }

            for (var i = 1; i < points.Count; i++)
            {
                var previous = points[i - 1];
                var current = points[i];
                var midX = (previous.X + current.X) / 2f;
                var midY = (previous.Y + current.Y) / 2f;
                path.QuadTo(previous.X, previous.Y, midX, midY);
            }

            var last = points[^1];
            path.LineTo(last.X, last.Y);
            path.LineTo(last.X, baselineY);
            path.Close();
            return path;
        }

        private static void DrawEmpty(ICanvas canvas, RectF rect)
        {
            var muted = TryGetColor("Muted") ?? Colors.Gray;
            canvas.FontColor = muted;
            canvas.FontSize = 12;
            var lang = Preferences.Default.Get("app_lang", "fr");
            var label = lang switch
            {
                "en" => "No data",
                "pt" => "Sem dados",
                "es" => "Sin datos",
                _ => "Pas de données",
            };
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
