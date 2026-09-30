using System.Windows;
using System.Windows.Media;

namespace DiskScope.Controls;

public class RealtimeMetricGraph : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IReadOnlyList<double>),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineColorProperty =
        DependencyProperty.Register(
            nameof(LineColor),
            typeof(Color),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0x67, 0xC0), FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(1.3, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue),
            typeof(double),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(
            nameof(MinValue),
            typeof(double),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxPointsProperty =
        DependencyProperty.Register(
            nameof(MaxPoints),
            typeof(int),
            typeof(RealtimeMetricGraph),
            new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    private Pen? _cachedLinePen;
    private static readonly Pen GridPen;
    private static readonly Pen BasePen;

    static RealtimeMetricGraph()
    {
        // Grid lines: extremely subtle dashed slate
        var gridBrush = new SolidColorBrush(Color.FromArgb(32, 0x0F, 0x17, 0x2A));
        gridBrush.Freeze();
        GridPen = new Pen(gridBrush, 1.0)
        {
            DashStyle = new DashStyle(new double[] { 3, 3 }, 0)
        };
        GridPen.Freeze();

        // Baseline: faint solid line
        var baseBrush = new SolidColorBrush(Color.FromArgb(48, 0x0F, 0x17, 0x2A));
        baseBrush.Freeze();
        BasePen = new Pen(baseBrush, 1.0);
        BasePen.Freeze();
    }

    public RealtimeMetricGraph()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public double MinValue
    {
        get => (double)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public int MaxPoints
    {
        get => (int)GetValue(MaxPointsProperty);
        set => SetValue(MaxPointsProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RealtimeMetricGraph graph)
        {
            graph._cachedLinePen = null;
        }
    }

    private Pen GetLinePen()
    {
        if (_cachedLinePen == null)
        {
            var brush = new SolidColorBrush(LineColor);
            brush.Freeze();
            var pen = new Pen(brush, Math.Max(0.5, StrokeThickness))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            _cachedLinePen = pen;
        }
        return _cachedLinePen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 4 || height <= 4) return;

        // Subtle horizontal guide lines (25%, 50%, 75%)
        dc.DrawLine(GridPen, new Point(0, height * 0.25), new Point(width, height * 0.25));
        dc.DrawLine(GridPen, new Point(0, height * 0.50), new Point(width, height * 0.50));
        dc.DrawLine(GridPen, new Point(0, height * 0.75), new Point(width, height * 0.75));

        // Baseline
        dc.DrawLine(BasePen, new Point(0, height - 1), new Point(width, height - 1));

        var values = Values;
        if (values == null || values.Count == 0) return;

        int count = values.Count;
        double min = MinValue;
        double max = MaxValue > min ? MaxValue : min + 1.0;
        double range = max - min;
        int maxPoints = Math.Max(MaxPoints, 2);

        double step = width / (maxPoints - 1.0);
        double usableHeight = Math.Max(height - 4, 1.0);

        if (count == 1)
        {
            double y = height - 2 - ((Math.Clamp(values[0], min, max) - min) / range) * usableHeight;
            dc.DrawLine(GetLinePen(), new Point(width - 4, y), new Point(width, y));
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double startX = width - (count - 1) * step;
            double startY = height - 2 - ((Math.Clamp(values[0], min, max) - min) / range) * usableHeight;
            ctx.BeginFigure(new Point(startX, startY), false, false);

            for (int i = 1; i < count; i++)
            {
                double x = width - (count - 1 - i) * step;
                double y = height - 2 - ((Math.Clamp(values[i], min, max) - min) / range) * usableHeight;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(null, GetLinePen(), geometry);
    }
}
