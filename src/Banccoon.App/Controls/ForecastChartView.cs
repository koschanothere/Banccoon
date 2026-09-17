using System.Collections.Specialized;
using System.Globalization;
using Banccoon.App.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.Controls;

public sealed class ForecastChartView : GraphicsView
{
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(
        nameof(Points),
        typeof(IEnumerable<ForecastChartPointViewModel>),
        typeof(ForecastChartView),
        Enumerable.Empty<ForecastChartPointViewModel>(),
        propertyChanged: OnPointsChanged);

    public static readonly BindableProperty SelectedPointProperty = BindableProperty.Create(
        nameof(SelectedPoint),
        typeof(ForecastChartPointViewModel),
        typeof(ForecastChartView),
        default(ForecastChartPointViewModel),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedPointChanged);

    private readonly ForecastChartDrawable chartDrawable = new();
    private INotifyCollectionChanged? observedPoints;

    public ForecastChartView()
    {
        Drawable = chartDrawable;
        BackgroundColor = Colors.Transparent;
        StartInteraction += OnInteraction;
        DragInteraction += OnInteraction;
    }

    public IEnumerable<ForecastChartPointViewModel> Points
    {
        get => (IEnumerable<ForecastChartPointViewModel>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public ForecastChartPointViewModel? SelectedPoint
    {
        get => (ForecastChartPointViewModel?)GetValue(SelectedPointProperty);
        set => SetValue(SelectedPointProperty, value);
    }

    private static void OnPointsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (ForecastChartView)bindable;
        chart.ObservePoints(oldValue, newValue);
        chart.RefreshDrawablePoints();
    }

    private static void OnSelectedPointChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (ForecastChartView)bindable;
        chart.chartDrawable.SelectedPoint = newValue as ForecastChartPointViewModel;
        chart.Invalidate();
    }

    private void ObservePoints(object? oldValue, object? newValue)
    {
        if (observedPoints is not null)
        {
            observedPoints.CollectionChanged -= OnPointsCollectionChanged;
        }

        observedPoints = newValue as INotifyCollectionChanged;
        if (observedPoints is not null)
        {
            observedPoints.CollectionChanged += OnPointsCollectionChanged;
        }
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDrawablePoints();
    }

    private void RefreshDrawablePoints()
    {
        chartDrawable.Points = Points?.ToArray() ?? Array.Empty<ForecastChartPointViewModel>();
        chartDrawable.SelectedPoint = SelectedPoint;
        Invalidate();
    }

    private void OnInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
        {
            return;
        }

        SelectNearestPoint(e.Touches[0]);
    }

    private void SelectNearestPoint(PointF touchPoint)
    {
        var points = chartDrawable.Points;
        if (points.Count == 0)
        {
            return;
        }

        var plot = ForecastChartDrawable.GetPlotArea((float)Width, (float)Height);
        var plotLeft = plot.X;
        var plotRight = plot.X + plot.Width;
        var clampedX = Math.Clamp(touchPoint.X, plotLeft, plotRight);
        var ratio = plot.Width <= 0f ? 0f : (clampedX - plotLeft) / plot.Width;
        var index = points.Count == 1
            ? 0
            : (int)Math.Round(ratio * (points.Count - 1), MidpointRounding.AwayFromZero);

        SelectedPoint = points[Math.Clamp(index, 0, points.Count - 1)];
    }
}

internal sealed class ForecastChartDrawable : IDrawable
{
    private static readonly Color AxisColor = Color.FromArgb("#DDE5DD");
    private static readonly Color GridColor = Color.FromArgb("#EEF3EF");
    private static readonly Color TextColor = Color.FromArgb("#14201A");
    private static readonly Color MutedTextColor = Color.FromArgb("#5A675E");
    private static readonly Color AccentColor = Color.FromArgb("#2E8B57");
    private static readonly Color AccentSoftColor = Color.FromArgb("#DDEEE4");
    private static readonly Color RoseColor = Color.FromArgb("#B55B67");

    public IReadOnlyList<ForecastChartPointViewModel> Points { get; set; } = Array.Empty<ForecastChartPointViewModel>();

    public ForecastChartPointViewModel? SelectedPoint { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.Antialias = true;

        if (dirtyRect.Width < 80f || dirtyRect.Height < 80f)
        {
            return;
        }

        if (Points.Count == 0)
        {
            DrawEmptyState(canvas, dirtyRect);
            return;
        }

        var plot = GetPlotArea(dirtyRect.Width, dirtyRect.Height);
        var minBalance = Points.Min(point => point.Balance);
        var maxBalance = Points.Max(point => point.Balance);
        if (minBalance == maxBalance)
        {
            minBalance -= 1m;
            maxBalance += 1m;
        }

        var padding = Math.Max(1m, (maxBalance - minBalance) * 0.08m);
        minBalance -= padding;
        maxBalance += padding;

        DrawGrid(canvas, plot, minBalance, maxBalance);
        DrawCurrentDateMarker(canvas, plot);
        DrawProjectionLine(canvas, plot, minBalance, maxBalance);
        DrawEventDots(canvas, plot, minBalance, maxBalance);
        DrawSelectedPoint(canvas, plot, minBalance, maxBalance, dirtyRect);
        DrawDateLabels(canvas, plot, dirtyRect);
    }

    public static RectF GetPlotArea(float width, float height)
    {
        return new RectF(54f, 16f, Math.Max(1f, width - 76f), Math.Max(1f, height - 48f));
    }

    private static void DrawEmptyState(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = MutedTextColor;
        canvas.FontSize = 13f;
        canvas.DrawString(
            "Add included accounts and scheduled items to see the projection.",
            dirtyRect.X,
            dirtyRect.Y,
            dirtyRect.Width,
            dirtyRect.Height,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawGrid(ICanvas canvas, RectF plot, decimal minBalance, decimal maxBalance)
    {
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1f;
        canvas.FontSize = 11f;
        canvas.FontColor = MutedTextColor;

        for (var index = 0; index <= 4; index++)
        {
            var ratio = index / 4f;
            var y = plot.Y + plot.Height - (plot.Height * ratio);
            canvas.DrawLine(plot.X, y, plot.X + plot.Width, y);

            var value = minBalance + ((maxBalance - minBalance) * (decimal)ratio);
            canvas.DrawString(
                value.ToString("N0", CultureInfo.CurrentCulture),
                0f,
                y - 8f,
                plot.X - 8f,
                16f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        canvas.StrokeColor = AxisColor;
        canvas.DrawLine(plot.X, plot.Y + plot.Height, plot.X + plot.Width, plot.Y + plot.Height);
    }

    private void DrawProjectionLine(ICanvas canvas, RectF plot, decimal minBalance, decimal maxBalance)
    {
        var path = new PathF();
        for (var index = 0; index < Points.Count; index++)
        {
            var point = Points[index];
            var x = GetX(plot, index);
            var y = GetY(plot, point.Balance, minBalance, maxBalance);
            if (index == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.StrokeColor = AccentColor;
        canvas.StrokeSize = 3f;
        canvas.DrawPath(path);
    }

    private void DrawCurrentDateMarker(ICanvas canvas, RectF plot)
    {
        var currentIndex = Points
            .Select((point, index) => new { point, index })
            .FirstOrDefault(pair => pair.point.IsCurrentDate)
            ?.index;
        if (currentIndex is null)
        {
            return;
        }

        var x = GetX(plot, currentIndex.Value);
        canvas.StrokeColor = TextColor;
        canvas.StrokeSize = 1.25f;
        canvas.StrokeDashPattern = new[] { 4f, 4f };
        canvas.DrawLine(x, plot.Y, x, plot.Y + plot.Height);
        canvas.StrokeDashPattern = Array.Empty<float>();

        canvas.FontColor = TextColor;
        canvas.FontSize = 10f;
        canvas.DrawString(
            "Today",
            x - 28f,
            plot.Y,
            56f,
            16f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawEventDots(ICanvas canvas, RectF plot, decimal minBalance, decimal maxBalance)
    {
        for (var index = 0; index < Points.Count; index++)
        {
            var point = Points[index];
            if (point.EventSummaries.Count == 0)
            {
                continue;
            }

            var x = GetX(plot, index);
            var y = GetY(plot, point.Balance, minBalance, maxBalance);
            canvas.FillColor = point.Balance < 0m ? RoseColor : AccentSoftColor;
            canvas.FillCircle(x, y, 4.5f);
            canvas.StrokeColor = AccentColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(x, y, 4.5f);
        }
    }

    private void DrawSelectedPoint(ICanvas canvas, RectF plot, decimal minBalance, decimal maxBalance, RectF dirtyRect)
    {
        var selected = SelectedPoint ?? Points.OrderBy(point => point.Balance).FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        var selectedIndex = Points
            .Select((point, index) => new { point, index })
            .FirstOrDefault(pair => pair.point.Date == selected.Date)
            ?.index ?? 0;
        var x = GetX(plot, selectedIndex);
        var y = GetY(plot, selected.Balance, minBalance, maxBalance);

        canvas.StrokeColor = AccentColor;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(x, plot.Y, x, plot.Y + plot.Height);
        canvas.FillColor = AccentColor;
        canvas.FillCircle(x, y, 6f);
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(x, y, 6f);

        var calloutWidth = Math.Min(260f, dirtyRect.Width - 24f);
        var calloutHeight = 68f;
        var calloutX = x + calloutWidth + 14f > dirtyRect.Width
            ? dirtyRect.Width - calloutWidth - 10f
            : Math.Max(10f, x + 14f);
        var calloutY = Math.Max(8f, y - calloutHeight - 12f);

        canvas.FillColor = Colors.White;
        canvas.FillRoundedRectangle(calloutX, calloutY, calloutWidth, calloutHeight, 8f);
        canvas.StrokeColor = AxisColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(calloutX, calloutY, calloutWidth, calloutHeight, 8f);

        canvas.FontColor = TextColor;
        canvas.FontSize = 12f;
        canvas.DrawString(selected.DateText, calloutX + 10f, calloutY + 8f, calloutWidth - 20f, 16f, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.FontSize = 15f;
        canvas.DrawString(selected.BalanceText, calloutX + 10f, calloutY + 25f, calloutWidth - 20f, 20f, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.FontColor = MutedTextColor;
        canvas.FontSize = 11f;
        canvas.DrawString(TrimForCallout(selected.EventsText), calloutX + 10f, calloutY + 46f, calloutWidth - 20f, 16f, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private void DrawDateLabels(ICanvas canvas, RectF plot, RectF dirtyRect)
    {
        var labelIndexes = Points.Count switch
        {
            1 => new[] { 0 },
            2 => new[] { 0, 1 },
            _ => new[] { 0, Points.Count / 2, Points.Count - 1 }
        };

        canvas.FontColor = MutedTextColor;
        canvas.FontSize = 11f;
        foreach (var index in labelIndexes.Distinct())
        {
            var x = GetX(plot, index);
            var alignment = index == 0
                ? HorizontalAlignment.Left
                : index == Points.Count - 1
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center;
            var labelX = index == 0
                ? plot.X
                : index == Points.Count - 1
                    ? x - 64f
                    : x - 32f;

            canvas.DrawString(
                Points[index].ShortDateText,
                labelX,
                dirtyRect.Height - 24f,
                64f,
                18f,
                alignment,
                VerticalAlignment.Center);
        }
    }

    private float GetX(RectF plot, int index)
    {
        return Points.Count == 1
            ? plot.X + (plot.Width / 2f)
            : plot.X + (plot.Width * index / (Points.Count - 1));
    }

    private static float GetY(RectF plot, decimal balance, decimal minBalance, decimal maxBalance)
    {
        var ratio = (float)((balance - minBalance) / (maxBalance - minBalance));
        return plot.Y + plot.Height - (plot.Height * ratio);
    }

    private static string TrimForCallout(string text)
    {
        return text.Length <= 58 ? text : $"{text[..55]}...";
    }
}
