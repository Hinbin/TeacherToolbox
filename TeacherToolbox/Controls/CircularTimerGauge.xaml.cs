using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;
using Windows.UI;

namespace TeacherToolbox.Controls
{
    public sealed partial class CircularTimerGauge : UserControl
    {
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(CircularTimerGauge), new PropertyMetadata(0d, OnTimerVisualPropertyChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CircularTimerGauge), new PropertyMetadata(60d, OnTimerVisualPropertyChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularTimerGauge), new PropertyMetadata(0d, OnTimerVisualPropertyChanged));

        public static readonly DependencyProperty TickSpacingProperty =
            DependencyProperty.Register(nameof(TickSpacing), typeof(double), typeof(CircularTimerGauge), new PropertyMetadata(5d));

        public static readonly DependencyProperty RingThicknessProperty =
            DependencyProperty.Register(nameof(RingThickness), typeof(double), typeof(CircularTimerGauge), new PropertyMetadata(22d, OnTimerVisualPropertyChanged));

        public static readonly DependencyProperty TrailBrushProperty =
            DependencyProperty.Register(nameof(TrailBrush), typeof(Brush), typeof(CircularTimerGauge), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 0x5b, 0x34, 0x93))));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        // Kept for compatibility with the previous RadialGauge-backed timer code.
        public double TickSpacing
        {
            get => (double)GetValue(TickSpacingProperty);
            set => SetValue(TickSpacingProperty, value);
        }

        public double RingThickness
        {
            get => (double)GetValue(RingThicknessProperty);
            set => SetValue(RingThicknessProperty, value);
        }

        public Brush TrailBrush
        {
            get => (Brush)GetValue(TrailBrushProperty);
            set => SetValue(TrailBrushProperty, value);
        }

        public CircularTimerGauge()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateRing();
        }

        private static void OnTimerVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CircularTimerGauge)d).UpdateRing();
        }

        private void CircularTimerGauge_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRing();
        }

        private void UpdateRing()
        {
            if (trackRing == null || progressRing == null)
            {
                return;
            }

            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0)
            {
                return;
            }

            double thickness = Math.Max(4, RingThickness);
            double inset = thickness / 2;
            trackRing.StrokeThickness = thickness;
            trackRing.Opacity = 0.35;
            trackRing.Data = CreateFullCircleGeometry(new Point(size / 2, size / 2), (size / 2) - inset);

            progressRing.StrokeThickness = thickness;
            progressRing.Data = CreateProgressGeometry(size, inset, GetProgress());
        }

        private double GetProgress()
        {
            double range = Maximum - Minimum;
            if (range <= 0)
            {
                return 0;
            }

            return Math.Clamp((Value - Minimum) / range, 0, 1);
        }

        private static Geometry CreateProgressGeometry(double size, double inset, double progress)
        {
            if (progress <= 0)
            {
                return new PathGeometry();
            }

            double radius = (size / 2) - inset;
            Point center = new(size / 2, size / 2);

            if (progress >= 0.999)
            {
                return CreateFullCircleGeometry(center, radius);
            }

            const double startAngle = -90;
            double endAngle = startAngle + (progress * 360);
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
                IsLargeArc = progress > 0.5
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Geometry CreateFullCircleGeometry(Point center, double radius)
        {
            Point top = PointOnCircle(center, radius, -90);
            Point bottom = PointOnCircle(center, radius, 90);

            var figure = new PathFigure
            {
                StartPoint = top,
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = bottom,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = true
            });

            figure.Segments.Add(new ArcSegment
            {
                Point = top,
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
