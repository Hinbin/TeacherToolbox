using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using Windows.Foundation;

namespace TeacherToolbox.Controls
{
    public sealed class RadialGauge : UserControl
    {
        public static readonly DependencyProperty MinAngleProperty =
            DependencyProperty.Register(nameof(MinAngle), typeof(double), typeof(RadialGauge), new PropertyMetadata(0d, OnVisualPropertyChanged));

        public static readonly DependencyProperty MaxAngleProperty =
            DependencyProperty.Register(nameof(MaxAngle), typeof(double), typeof(RadialGauge), new PropertyMetadata(0d, OnVisualPropertyChanged));

        public static readonly DependencyProperty ScaleWidthProperty =
            DependencyProperty.Register(nameof(ScaleWidth), typeof(double), typeof(RadialGauge), new PropertyMetadata(40d, OnVisualPropertyChanged));

        public static readonly DependencyProperty ScalePaddingProperty =
            DependencyProperty.Register(nameof(ScalePadding), typeof(double), typeof(RadialGauge), new PropertyMetadata(0d, OnVisualPropertyChanged));

        public static readonly DependencyProperty TrailBrushProperty =
            DependencyProperty.Register(nameof(TrailBrush), typeof(Brush), typeof(RadialGauge), new PropertyMetadata(null, OnVisualPropertyChanged));

        private readonly Path _arcPath;

        public bool IsInteractive { get; set; }
        public double NeedleLength { get; set; }
        public string ValueStringFormat { get; set; }
        public double TickSpacing { get; set; }
        public Brush ScaleBrush { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Value { get; set; }
        public double NeedleWidth { get; set; }

        public double MinAngle
        {
            get => (double)GetValue(MinAngleProperty);
            set => SetValue(MinAngleProperty, value);
        }

        public double MaxAngle
        {
            get => (double)GetValue(MaxAngleProperty);
            set => SetValue(MaxAngleProperty, value);
        }

        public double ScaleWidth
        {
            get => (double)GetValue(ScaleWidthProperty);
            set => SetValue(ScaleWidthProperty, value);
        }

        public double ScalePadding
        {
            get => (double)GetValue(ScalePaddingProperty);
            set => SetValue(ScalePaddingProperty, value);
        }

        public Brush TrailBrush
        {
            get => (Brush)GetValue(TrailBrushProperty);
            set => SetValue(TrailBrushProperty, value);
        }

        public RadialGauge()
        {
            IsHitTestVisible = false;
            _arcPath = new Path
            {
                IsHitTestVisible = false,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeLineJoin = PenLineJoin.Round
            };

            Content = _arcPath;
            Loaded += (_, _) => UpdateArc();
            SizeChanged += (_, _) => UpdateArc();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RadialGauge)d).UpdateArc();
        }

        private void UpdateArc()
        {
            if (_arcPath == null)
            {
                return;
            }

            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0)
            {
                return;
            }

            double thickness = Math.Max(1, ScaleWidth);
            double radius = (size / 2) - ScalePadding - (thickness / 2);
            if (radius <= 0)
            {
                _arcPath.Data = new PathGeometry();
                return;
            }

            _arcPath.Stroke = TrailBrush;
            _arcPath.StrokeThickness = thickness;
            _arcPath.Data = CreateArcGeometry(new Point(size / 2, size / 2), radius, MinAngle, MaxAngle);
        }

        private static Geometry CreateArcGeometry(Point center, double radius, double minAngle, double maxAngle)
        {
            double span = Math.Clamp(maxAngle - minAngle, 0, 360);
            if (span <= 0)
            {
                return new PathGeometry();
            }

            if (span >= 359.9)
            {
                return CreateFullCircleGeometry(center, radius, minAngle - 90);
            }

            double startAngle = minAngle - 90;
            double endAngle = startAngle + span;
            Point start = PointOnCircle(center, radius, startAngle);
            Point end = PointOnCircle(center, radius, endAngle);

            var figure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = span > 180
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Geometry CreateFullCircleGeometry(Point center, double radius, double startAngle)
        {
            Point start = PointOnCircle(center, radius, startAngle);
            Point halfway = PointOnCircle(center, radius, startAngle + 180);

            var figure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = halfway,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = true
            });

            figure.Segments.Add(new ArcSegment
            {
                Point = start,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = true
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Point PointOnCircle(Point center, double radius, double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180;
            return new Point(
                center.X + (radius * Math.Cos(angleRadians)),
                center.Y + (radius * Math.Sin(angleRadians)));
        }
    }
}
