using Microsoft.Maui.Graphics;

namespace NutritionTracker.Controls;

public class MacroDonutChart : GraphicsView
{
    public static readonly BindableProperty ValuesProperty = BindableProperty.Create(
        nameof(Values),
        typeof(IList<double>),
        typeof(MacroDonutChart),
        defaultValue: Array.Empty<double>(),
        propertyChanged: (b, o, n) => ((MacroDonutChart)b).Invalidate());

    public static readonly BindableProperty LabelsProperty = BindableProperty.Create(
        nameof(Labels),
        typeof(IList<string>),
        typeof(MacroDonutChart),
        defaultValue: Array.Empty<string>(),
        propertyChanged: (b, o, n) => ((MacroDonutChart)b).Invalidate());

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

    public MacroDonutChart()
    {
        Drawable = new MacroDonutDrawable(this);
    }

    private sealed class MacroDonutDrawable : IDrawable
    {
        private readonly MacroDonutChart _chart;

        public MacroDonutDrawable(MacroDonutChart chart)
        {
            _chart = chart;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = _chart.Values ?? Array.Empty<double>();
            var total = values.Sum(v => Math.Max(0, v));

            var muted = TryGetColor("Muted") ?? Colors.Gray;
            var card = TryGetColor("Card") ?? Colors.White;
            var text = TryGetColor("Text") ?? Colors.Black;
            var protein = TryGetColor("MacroProtein") ?? Colors.Teal;
            var carbs = TryGetColor("MacroCarbs") ?? Colors.DodgerBlue;
            var fat = TryGetColor("MacroFat") ?? Colors.Goldenrod;
            var qualityGood = Colors.MediumSeaGreen;
            var qualityMedium = Colors.Goldenrod;
            var qualityLow = Colors.IndianRed;

            var colors = IsQualitySplit(_chart.Labels)
                ? new[] { qualityGood, qualityMedium, qualityLow }
                : new[] { protein, carbs, fat };

            var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var stroke = Math.Max(14f, size * 0.18f);
            var inset = stroke / 2f + 6f;
            var ring = new RectF(dirtyRect.X + inset, dirtyRect.Y + inset, size - inset * 2f, size - inset * 2f);

            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Butt;
            canvas.Antialias = true;

            canvas.StrokeColor = muted.WithAlpha(0.25f);
            canvas.DrawArc(ring, 0, 360, false, false);

            if (total > 0.0001)
            {
                float start = -90f;
                for (var i = 0; i < Math.Min(values.Count, 3); i++)
                {
                    var v = Math.Max(0, values[i]);
                    if (v <= 0) continue;

                    var sweep = (float)(360d * (v / total));
                    canvas.StrokeColor = colors[i];
                    canvas.DrawArc(ring, start, sweep, false, false);
                    start += sweep;
                }
            }

            var innerInset = stroke + 8f;
            var innerRect = new RectF(ring.X + innerInset / 2f, ring.Y + innerInset / 2f, ring.Width - innerInset, ring.Height - innerInset);
            canvas.FillColor = card;
            canvas.FillEllipse(innerRect);

            canvas.FontColor = text;
            canvas.FontSize = 11;
            var centerTop = ring.Center.Y - 16;
            canvas.DrawString("kcal", ring.Center.X - 20, centerTop, 40, 14, HorizontalAlignment.Center, VerticalAlignment.Center);

            canvas.FontSize = 14;
            canvas.FontColor = text;
            var valueText = total > 0 ? Math.Round(total).ToString() : "0";
            canvas.DrawString(valueText, ring.Center.X - 26, centerTop + 14, 52, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        private static bool IsQualitySplit(IList<string>? labels)
        {
            if (labels == null || labels.Count < 3)
                return false;

            var first = (labels[0] ?? "").ToLowerInvariant();
            var second = (labels[1] ?? "").ToLowerInvariant();
            var third = (labels[2] ?? "").ToLowerInvariant();

            var isGood = first.Contains("good") || first.Contains("bonne") || first.Contains("boa") || first.Contains("buena");
            var isMedium = second.Contains("medium") || second.Contains("moy") || second.Contains("media") || second.Contains("média");
            var isLow = third.Contains("low") || third.Contains("faible") || third.Contains("baixa") || third.Contains("baja");

            return isGood && isMedium && isLow;
        }

        private static Color? TryGetColor(string key)
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var obj) == true && obj is Color c)
                return c;
            return null;
        }
    }
}
