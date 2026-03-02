using Microsoft.Maui.Graphics;

namespace NutritionTracker.Controls;

public class BarChart : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values),
        typeof(IList<double>),
        typeof(BarChart),
        defaultValue: Array.Empty<double>(),
        propertyChanged: (b, o, n) => ((BarChart)b).Invalidate());

    public static readonly BindableProperty LabelsProperty = BindableProperty.Create(
        nameof(Labels),
        typeof(IList<string>),
        typeof(BarChart),
        defaultValue: Array.Empty<string>(),
        propertyChanged: (b, o, n) => ((BarChart)b).Invalidate());

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

    public BarChart()
    {
        Drawable = new BarChartDrawable(this);
    }

    private sealed class BarChartDrawable : IDrawable
    {
        private readonly BarChart _chart;

        public BarChartDrawable(BarChart chart) => _chart = chart;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = _chart.Values ?? Array.Empty<double>();
            if (values.Count == 0)
            {
                DrawEmpty(canvas, dirtyRect);
                return;
            }

            var accent = TryGetColor("Accent") ?? Colors.DodgerBlue;
            var muted = TryGetColor("Muted") ?? Colors.Gray;
            var text = TryGetColor("Text") ?? Colors.Black;

            var leftPad = 10f;
            var rightPad = 10f;
            var topPad = 12f;
            var bottomPad = 26f;

            var plot = new RectF(
                dirtyRect.X + leftPad,
                dirtyRect.Y + topPad,
                Math.Max(1, dirtyRect.Width - leftPad - rightPad),
                Math.Max(1, dirtyRect.Height - topPad - bottomPad));

            var max = Math.Max(1, values.Max());
            var count = values.Count;
            var slotWidth = plot.Width / count;
            var barWidth = Math.Max(6f, Math.Min(28f, slotWidth * 0.62f));

            canvas.StrokeColor = muted.WithAlpha(0.3f);
            canvas.StrokeSize = 1;
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Top + (plot.Height * i / 4f);
                canvas.DrawLine(plot.Left, y, plot.Right, y);
            }

            for (var i = 0; i < count; i++)
            {
                var v = Math.Max(0, values[i]);
                var h = (float)(v / max * plot.Height);
                var x = plot.Left + (slotWidth * i) + ((slotWidth - barWidth) / 2f);
                var y = plot.Bottom - h;

                var color = i == count - 1 ? accent : accent.WithAlpha(0.65f);
                canvas.FillColor = color;
                canvas.FillRoundedRectangle(x, y, barWidth, Math.Max(2f, h), 6f);
            }

            var labels = _chart.Labels ?? Array.Empty<string>();
            if (labels.Count == count)
            {
                canvas.FontColor = text;
                canvas.FontSize = 10;
                if (count == 1)
                {
                    DrawLabel(canvas, labels[0], plot.Left + plot.Width / 2f, plot.Bottom + 4f);
                }
                else
                {
                    DrawLabel(canvas, labels[0], plot.Left + slotWidth / 2f, plot.Bottom + 4f);
                    if (count > 2)
                        DrawLabel(canvas, labels[count / 2], plot.Left + slotWidth * (count / 2f) + slotWidth / 2f, plot.Bottom + 4f);
                    DrawLabel(canvas, labels[count - 1], plot.Left + slotWidth * (count - 1) + slotWidth / 2f, plot.Bottom + 4f);
                }
            }

            canvas.FontColor = text;
            canvas.FontSize = 11;
            canvas.DrawString($"{Math.Round(values[count - 1])}", plot.Right - 34, plot.Top, 34, 14, HorizontalAlignment.Right, VerticalAlignment.Top);
        }

        private static void DrawLabel(ICanvas canvas, string text, float x, float y)
            => canvas.DrawString(text, x - 28, y, 56, 14, HorizontalAlignment.Center, VerticalAlignment.Top);

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

        private static Color? TryGetColor(string key)
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var obj) == true && obj is Color c)
                return c;
            return null;
        }
    }
}
