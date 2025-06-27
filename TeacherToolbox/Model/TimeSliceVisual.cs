using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using TeacherToolbox.Controls;

namespace TeacherToolbox.Model
{
    /// <summary>
    /// Visual representation of a time slice for binding in XAML
    /// </summary>
    public class TimeSliceVisual : ObservableObject
    {
        private string _name;
        private double _minAngle;
        private double _maxAngle;
        private double _width;
        private double _height;
        private double _left;
        private double _top;
        private int _zIndex;
        private Brush _trailBrush;
        private double _scalePadding;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public double MinAngle
        {
            get => _minAngle;
            set => SetProperty(ref _minAngle, value);
        }

        public double MaxAngle
        {
            get => _maxAngle;
            set => SetProperty(ref _maxAngle, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        public int ZIndex
        {
            get => _zIndex;
            set => SetProperty(ref _zIndex, value);
        }

        public Brush TrailBrush
        {
            get => _trailBrush;
            set => SetProperty(ref _trailBrush, value);
        }

        public double ScalePadding
        {
            get => _scalePadding;
            set => SetProperty(ref _scalePadding, value);
        }

        public TimeSliceVisual(TimeSlice timeSlice, int radialLevel, Brush brush)
        {
            Name = timeSlice.Name;
            TrailBrush = brush;

            // Set visual properties based on radial level
            if (radialLevel == (int)RadialLevel.Inner)
            {
                Width = 200;
                Height = 200;
                Left = 0;
                Top = 0;
                ZIndex = 1;
                ScalePadding = 5;
            }
            else
            {
                Width = 140;
                Height = 140;
                Left = 30;
                Top = 30;
                ZIndex = 2;
                ScalePadding = 0;
            }

            UpdateAngles(timeSlice);
        }

        public void UpdateAngles(TimeSlice timeSlice)
        {
            int startFiveMinuteInterval = timeSlice.StartMinute / 5;
            int endFiveMinuteInterval = (timeSlice.StartMinute + timeSlice.Duration) / 5;

            MinAngle = startFiveMinuteInterval * 30;
            MaxAngle = endFiveMinuteInterval * 30;
        }
    }
}