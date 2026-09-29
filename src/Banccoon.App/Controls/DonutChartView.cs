using System.Collections.Specialized;
using System.Windows.Input;
using Banccoon.App.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Banccoon.App.Controls;

// Renders AnalyticsCategoryRowViewModel.Amount/Color as a hollow donut, and highlights whichever
// slice the pointer is over. Windows/Mac Catalyst only (PointerGestureRecognizer isn't available
// on touch platforms), which matches this app's Windows-only target. A click (pointer released
// over a slice) runs SegmentClickedCommand with that slice - the Analytics card uses it to open a
// parent category's breakdown in a second donut. Uses the same PointerGestureRecognizer as the
// hover rather than adding a TapGestureRecognizer next to it.
public sealed class DonutChartView : GraphicsView
{
    public static readonly BindableProperty SegmentsProperty = BindableProperty.Create(
        nameof(Segments),
        typeof(IEnumerable<AnalyticsCategoryRowViewModel>),
        typeof(DonutChartView),
        Enumerable.Empty<AnalyticsCategoryRowViewModel>(),
        propertyChanged: OnSegmentsChanged);

    public static readonly BindableProperty HighlightedSegmentProperty = BindableProperty.Create(
        nameof(HighlightedSegment),
        typeof(AnalyticsCategoryRowViewModel),
        typeof(DonutChartView),
        default(AnalyticsCategoryRowViewModel),
        BindingMode.TwoWay,
        propertyChanged: OnHighlightedSegmentChanged);

    public static readonly BindableProperty SegmentClickedCommandProperty = BindableProperty.Create(
        nameof(SegmentClickedCommand),
        typeof(ICommand),
        typeof(DonutChartView),
        default(ICommand));

    private readonly DonutChartDrawable chartDrawable = new();
    private INotifyCollectionChanged? observedSegments;

    public DonutChartView()
    {
        Drawable = chartDrawable;
        BackgroundColor = Colors.Transparent;

        var pointerRecognizer = new PointerGestureRecognizer();
        pointerRecognizer.PointerMoved += OnPointerMoved;
        pointerRecognizer.PointerExited += OnPointerExited;
        pointerRecognizer.PointerReleased += OnPointerReleased;
        GestureRecognizers.Add(pointerRecognizer);
    }

    public IEnumerable<AnalyticsCategoryRowViewModel> Segments
    {
        get => (IEnumerable<AnalyticsCategoryRowViewModel>)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public AnalyticsCategoryRowViewModel? HighlightedSegment
    {
        get => (AnalyticsCategoryRowViewModel?)GetValue(HighlightedSegmentProperty);
        set => SetValue(HighlightedSegmentProperty, value);
    }

    public ICommand? SegmentClickedCommand
    {
        get => (ICommand?)GetValue(SegmentClickedCommandProperty);
        set => SetValue(SegmentClickedCommandProperty, value);
    }

    private static void OnSegmentsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (DonutChartView)bindable;
        chart.ObserveSegments(oldValue, newValue);
        chart.RefreshDrawableSegments();
    }

    private static void OnHighlightedSegmentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (DonutChartView)bindable;
        chart.chartDrawable.HighlightedSegment = newValue as AnalyticsCategoryRowViewModel;
        chart.Invalidate();
    }

    private void ObserveSegments(object? oldValue, object? newValue)
    {
        if (observedSegments is not null)
        {
            observedSegments.CollectionChanged -= OnSegmentsCollectionChanged;
        }

        observedSegments = newValue as INotifyCollectionChanged;
        if (observedSegments is not null)
        {
            observedSegments.CollectionChanged += OnSegmentsCollectionChanged;
        }
    }

    private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDrawableSegments();
    }

    private void RefreshDrawableSegments()
    {
        chartDrawable.Segments = Segments?.ToArray() ?? Array.Empty<AnalyticsCategoryRowViewModel>();
        chartDrawable.HighlightedSegment = HighlightedSegment;
        Invalidate();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        if (point is null)
        {
            return;
        }

        var segment = chartDrawable.HitTest(new PointF((float)point.Value.X, (float)point.Value.Y), (float)Width, (float)Height);
        if (!ReferenceEquals(segment, HighlightedSegment))
        {
            HighlightedSegment = segment;
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        if (point is null || SegmentClickedCommand is not { } command)
        {
            return;
        }

        var segment = chartDrawable.HitTest(new PointF((float)point.Value.X, (float)point.Value.Y), (float)Width, (float)Height);
        if (segment is not null && command.CanExecute(segment))
        {
            command.Execute(segment);
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        HighlightedSegment = null;
    }
}

internal sealed class DonutChartDrawable : IDrawable
{
    private const float InnerRadiusRatio = 0.6f;
    private const float HighlightOuterBoost = 4f;
    private const float StepsPerFullCircle = 120f;
    private static readonly Color EmptyRingColor = Color.FromArgb("#E7E9EC");

    public IReadOnlyList<AnalyticsCategoryRowViewModel> Segments { get; set; } = Array.Empty<AnalyticsCategoryRowViewModel>();

    public AnalyticsCategoryRowViewModel? HighlightedSegment { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.Antialias = true;

        var geometry = ComputeGeometry(dirtyRect.Width, dirtyRect.Height);
        if (geometry is not { } g)
        {
            return;
        }

        var total = Segments.Sum(segment => segment.Amount);
        if (total <= 0m || Segments.Count == 0)
        {
            canvas.FillColor = EmptyRingColor;
            canvas.FillPath(BuildRingSegmentPath(g.Center, g.InnerRadius, g.OuterRadius, 0f, 360f));
            return;
        }

        var cursor = 0f;
        foreach (var segment in Segments)
        {
            var sweep = (float)(segment.Amount / total) * 360f;
            var isHighlighted = HighlightedSegment is not null && ReferenceEquals(HighlightedSegment, segment);
            var isDimmed = HighlightedSegment is not null && !isHighlighted;
            var outerRadius = isHighlighted ? g.OuterRadius + HighlightOuterBoost : g.OuterRadius;

            canvas.FillColor = isDimmed ? segment.Color.WithAlpha(0.35f) : segment.Color;
            canvas.FillPath(BuildRingSegmentPath(g.Center, g.InnerRadius, outerRadius, cursor, cursor + sweep));

            cursor += sweep;
        }
    }

    // Consumed by DonutChartView on pointer-move - deliberately reuses the exact same angle
    // convention and geometry as Draw (see ComputeGeometry/AngleOf) so hovering always highlights
    // the slice actually under the cursor rather than one drawn at a slightly different angle.
    public AnalyticsCategoryRowViewModel? HitTest(PointF point, float width, float height)
    {
        var geometry = ComputeGeometry(width, height);
        if (geometry is not { } g || Segments.Count == 0)
        {
            return null;
        }

        var total = Segments.Sum(segment => segment.Amount);
        if (total <= 0m)
        {
            return null;
        }

        var dx = point.X - g.Center.X;
        var dy = point.Y - g.Center.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance < g.InnerRadius || distance > g.OuterRadius + HighlightOuterBoost)
        {
            return null;
        }

        var angle = AngleOf(dx, dy);
        var cursor = 0f;
        foreach (var segment in Segments)
        {
            var sweep = (float)(segment.Amount / total) * 360f;
            if (angle >= cursor && angle < cursor + sweep)
            {
                return segment;
            }

            cursor += sweep;
        }

        return Segments[^1];
    }

    private static Geometry? ComputeGeometry(float width, float height)
    {
        var size = Math.Min(width, height);
        if (size < 40f)
        {
            return null;
        }

        var center = new PointF(width / 2f, height / 2f);
        var outerRadius = (size / 2f) - HighlightOuterBoost - 2f;
        var innerRadius = outerRadius * InnerRadiusRatio;
        return new Geometry(center, innerRadius, outerRadius);
    }

    // Clock-face convention: 0 degrees is straight up, increasing clockwise as drawn on screen -
    // matches the everyday reading of a pie/donut chart. dx/dy are relative to the chart's center.
    private static float AngleOf(float dx, float dy)
    {
        var radians = Math.Atan2(dx, -dy);
        var degrees = radians * 180.0 / Math.PI;
        return (float)(degrees < 0 ? degrees + 360.0 : degrees);
    }

    private static PointF PointOnCircle(PointF center, float radius, float angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var x = center.X + (radius * (float)Math.Sin(radians));
        var y = center.Y - (radius * (float)Math.Cos(radians));
        return new PointF(x, y);
    }

    private static PathF BuildRingSegmentPath(PointF center, float innerRadius, float outerRadius, float startAngle, float endAngle)
    {
        var path = new PathF();
        var sweep = endAngle - startAngle;
        var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / 360f * StepsPerFullCircle));

        for (var i = 0; i <= steps; i++)
        {
            var angle = startAngle + (sweep * i / steps);
            var point = PointOnCircle(center, outerRadius, angle);
            if (i == 0)
            {
                path.MoveTo(point);
            }
            else
            {
                path.LineTo(point);
            }
        }

        for (var i = steps; i >= 0; i--)
        {
            var angle = startAngle + (sweep * i / steps);
            path.LineTo(PointOnCircle(center, innerRadius, angle));
        }

        path.Close();
        return path;
    }

    private readonly record struct Geometry(PointF Center, float InnerRadius, float OuterRadius);
}
