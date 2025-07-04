using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Model;
using TeacherToolbox.Services;
using TeacherToolbox.ViewModels;
using TeacherToolbox.Helpers;
using Windows.Foundation;

namespace TeacherToolbox.Tests.ViewModels
{
    [TestFixture]
    public class ClockViewModelTests
    {
        private Mock<ISettingsService> _mockSettingsService;
        private Mock<ISleepPreventer> _mockSleepPreventer;
        private Mock<ITimerService> _mockTimerService;
        private Mock<IThemeService> _mockThemeService;
        private ClockViewModel _viewModel;

        [SetUp]
        public void Setup()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSleepPreventer = new Mock<ISleepPreventer>(); ;
            _mockThemeService = new Mock<IThemeService>();

            // Setup default return values
            _mockSettingsService.Setup(s => s.GetCentreText()).Returns("Test Centre");
            _mockSettingsService.Setup(s => s.GetHasShownClockInstructions()).Returns(false);

            // Setup theme service - avoid creating real WinUI objects in tests
            _mockThemeService.Setup(t => t.IsDarkTheme).Returns(false);
            _mockThemeService.Setup(t => t.CurrentTheme).Returns(ElementTheme.Light);
            _mockThemeService.Setup(t => t.HandColorBrush).Returns((SolidColorBrush)null);

            // Create a smart timer mock that tracks its own state
            _mockTimerService = CreateSmartTimerMock();

            // Pass all mock objects to the constructor
            _viewModel = new ClockViewModel(
                _mockSettingsService.Object,
                _mockSleepPreventer.Object,
                _mockTimerService.Object,
                _mockThemeService.Object);
        }

        private Mock<ITimerService> CreateSmartTimerMock()
        {
            var mock = new Mock<ITimerService>();
            bool isEnabled = false;
            TimeSpan interval = TimeSpan.Zero;

            // Set up Interval property with backing field
            mock.SetupProperty(t => t.Interval);

            // Set up IsEnabled to return the tracked state
            mock.Setup(t => t.IsEnabled).Returns(() => isEnabled);

            // Set up Start to change the state
            mock.Setup(t => t.Start()).Callback(() => isEnabled = true);

            // Set up Stop to change the state
            mock.Setup(t => t.Stop()).Callback(() => isEnabled = false);

            return mock;
        }

        [TearDown]
        public void Cleanup()
        {
            _viewModel?.Dispose();
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ClockViewModel(null, _mockSleepPreventer.Object, null,null));
        }

        [Test]
        public void Constructor_WithValidParameters_InitializesSuccessfully()
        {
            // Act & Assert
            Assert.That(_viewModel, Is.Not.Null);

            // Verify timer was configured
            _mockTimerService.VerifySet(t => t.Interval = It.IsAny<TimeSpan>(), Times.Once);
        }

        [Test]
        public void Constructor_InitializesWithCorrectCentreText()
        {
            Assert.That(_viewModel.CentreText, Is.EqualTo("Test Centre"));
        }

        [Test]
        public void Constructor_CallsPreventSleep()
        {
            _mockSleepPreventer.Verify(s => s.PreventSleep(true), Times.Once);
        }

        [Test]
        public void Constructor_InitializesEmptyTimeSlices()
        {
            Assert.That(_viewModel.TimeSlices, Is.Not.Null);
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_HandlesMissingThemeService_GracefullyWithFallback()
        {
            // Test that ViewModel can handle null brush from theme service
            // This test verifies the ViewModel doesn't crash when theme service returns null

            // The ViewModel should have been constructed successfully even with null brush
            Assert.That(_viewModel, Is.Not.Null);

            // HandColorBrush should either be null or have a fallback value
            // The ViewModel should handle this gracefully
            Assert.DoesNotThrow(() => {
                var brush = _viewModel.HandColorBrush;
                // The brush can be null or a fallback - both are acceptable
            });
        }

        #endregion

        #region Time Management Tests

        [Test]
        public void TimeOffset_WhenSet_UpdatesCurrentTime()
        {
            var initialTime = _viewModel.CurrentTime;
            var offset = TimeSpan.FromHours(1);

            _viewModel.TimeOffset = offset;

            // Time should be approximately 1 hour ahead
            var timeDiff = _viewModel.CurrentTime - initialTime;
            Assert.That(timeDiff.TotalMinutes, Is.GreaterThan(59).And.LessThan(61));
        }

        [Test]
        public void DigitalTimeText_UpdatesWithCurrentTime()
        {
            Assert.That(_viewModel.DigitalTimeText, Is.Not.Null);
            Assert.That(_viewModel.DigitalTimeText, Does.Match(@"\d{1,2}:\d{2} (AM|PM)"));
        }

        #endregion

        #region Centre Text Tests

        [Test]
        public void CentreText_WhenSet_UpdatesSettingsService()
        {
            _viewModel.CentreText = "New Centre";

            _mockSettingsService.Verify(s => s.SetCentreText("New Centre"), Times.Once);
            Assert.That(_viewModel.CentreText, Is.EqualTo("New Centre"));
        }

        [Test]
        public void CentreText_WhenSetToSameValue_DoesNotUpdateSettings()
        {
            _mockSettingsService.Invocations.Clear();

            _viewModel.CentreText = "Test Centre"; // Same as initial value

            _mockSettingsService.Verify(s => s.SetCentreText(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Time Slice Management Tests

        [Test]
        public void AddGaugeCommand_AddsNewTimeSlice()
        {
            var point = new Point(150, 100); // Example point
            bool eventRaised = false;
            TimeSlice addedSlice = null;

            _viewModel.TimeSliceAdded += (s, slice) => {
                eventRaised = true;
                addedSlice = slice;
            };

            _viewModel.AddGaugeCommand.Execute(point);

            Assert.That(eventRaised, Is.True);
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1));
            Assert.That(addedSlice, Is.Not.Null);
        }

        [Test]
        public void AddGaugeCommand_DoesNotAddDuplicateTimeSlice()
        {
            var point = new Point(150, 100);

            // Add first slice
            _viewModel.AddGaugeCommand.Execute(point);
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1));

            // Try to add at same position - should not add another slice
            _viewModel.AddGaugeCommand.Execute(point);

            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1),
                "Should not create duplicate time slice at same position");
        }

        [Test]
        public void RemoveGaugeCommand_RemovesExistingTimeSlice()
        {
            // First add a time slice
            var point = new Point(150, 100);
            _viewModel.AddGaugeCommand.Execute(point);

            var sliceName = _viewModel.TimeSlices.First().Name;
            bool eventRaised = false;

            _viewModel.TimeSliceRemoved += (s, e) => eventRaised = true;

            _viewModel.RemoveGaugeCommand.Execute(sliceName);

            Assert.That(eventRaised, Is.True);
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(0));
        }

        [Test]
        public void FindTimeSliceAtPosition_ReturnsCorrectSlice()
        {
            // Add a time slice
            _viewModel.AddGaugeCommand.Execute(new Point(150, 100));
            var slice = _viewModel.TimeSlices.First();

            // Calculate the position parameters that should match the slice
            var timeData = GetMinutesFromCoordinate(new Point(150, 100));

            // Find at the same position
            var found = _viewModel.FindTimeSliceAtPosition(timeData[0], timeData[1]);

            Assert.That(found, Is.Not.Null);
            Assert.That(found.Name, Is.EqualTo(slice.Name));
        }

        [Test]
        public void FindTimeSliceAtPosition_ReturnsNullWhenNotFound()
        {
            var found = _viewModel.FindTimeSliceAtPosition(30, (int)RadialLevel.Inner);

            Assert.That(found, Is.Null);
        }

        #endregion

        #region Time Slice Extension Tests

        [Test]
        public void ExtendTimeSlice_ExtendsForward()
        {
            // Add initial slice at 0 minutes
            _viewModel.AddGaugeCommand.Execute(new Point(100, 30)); // Top of clock
            var slice = _viewModel.TimeSlices.First();
            var initialDuration = slice.Duration;

            // Extend to next 5-minute interval
            _viewModel.ExtendTimeSlice(slice.Name, 5, (int)RadialLevel.Inner);

            Assert.That(slice.Duration, Is.GreaterThan(initialDuration));
        }

        [Test]
        public void ExtendTimeSlice_DoesNotExtendIntoOccupiedSpace()
        {
            // Add two slices
            _viewModel.AddGaugeCommand.Execute(new Point(100, 30)); // 0 minutes
            _viewModel.AddGaugeCommand.Execute(new Point(170, 100)); // ~15 minutes

            var firstSlice = _viewModel.TimeSlices.First();
            var initialDuration = firstSlice.Duration;

            // Try to extend first slice into second slice's space
            _viewModel.ExtendTimeSlice(firstSlice.Name, 15, (int)RadialLevel.Inner);

            Assert.That(firstSlice.Duration, Is.EqualTo(initialDuration));
        }

        [Test]
        public void ExtendTimeSlice_PreventsCrossingHourBoundaryOverlap_SpecificScenario()
        {
            // Specific scenario from user:
            // 1. Create segment from 3 to 5 o'clock (15-25 minutes)
            _viewModel.AddGaugeCommand.Execute(new Point(170, 100)); // ~15 minutes  
            var firstSlice = _viewModel.TimeSlices.First();
            // Extend to 25 minutes
            _viewModel.ExtendTimeSlice(firstSlice.Name, 20, (int)RadialLevel.Inner);
            _viewModel.ExtendTimeSlice(firstSlice.Name, 25, (int)RadialLevel.Inner);

            Assert.That(firstSlice.StartMinute, Is.EqualTo(15));
            Assert.That(firstSlice.Duration, Is.GreaterThanOrEqualTo(10)); // Should cover 15-25

            // 2. Create new segment at 8 o'clock (40 minutes)
            _viewModel.AddGaugeCommand.Execute(new Point(20, 145)); // ~40 minutes
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(2));

            var secondSlice = _viewModel.TimeSlices.Last();

            // 3. Try to drag back to 2 o'clock (10 minutes) - this crosses hour boundary
            // The slice would go from 40 minutes through 0 to 10 minutes
            // This should be prevented as it would overlap with the 15-25 minute slice
            var initialSecondSliceStart = secondSlice.StartMinute;
            var initialSecondSliceDuration = secondSlice.Duration;

            // Try extending backwards across the hour boundary
            _viewModel.ExtendTimeSlice(secondSlice.Name, 10, (int)RadialLevel.Inner);

            // The slice should NOT have changed because it would create an overlap
            Assert.That(secondSlice.StartMinute, Is.EqualTo(initialSecondSliceStart),
                "Start minute should not change when overlap would occur");
            Assert.That(secondSlice.Duration, Is.EqualTo(initialSecondSliceDuration),
                "Duration should not change when overlap would occur");
        }

        [Test]
        public void ExtendTimeSlice_DoesNotExtendAcrossRadialLevels()
        {
            // Create a time slice in the outer radial level
            _viewModel.AddGaugeCommand.Execute(new Point(130, 80)); // Outer ring position
            var slice = _viewModel.TimeSlices.First();

            Assert.That(slice.RadialLevel, Is.EqualTo((int)RadialLevel.Outer));

            var initialDuration = slice.Duration;

            // Try to extend to a position but with inner radial level
            _viewModel.ExtendTimeSlice(slice.Name, 10, (int)RadialLevel.Inner);

            // The slice should not have been extended because radial levels don't match
            Assert.That(slice.Duration, Is.EqualTo(initialDuration),
                "Slice should not extend when radial level doesn't match");
        }

        #endregion

        #region Clock Instructions Tests

        [Test]
        public void HasShownClockInstructions_ReturnsValueFromSettings()
        {
            _mockSettingsService.Setup(s => s.GetHasShownClockInstructions()).Returns(true);

            var result = _viewModel.HasShownClockInstructions();

            Assert.That(result, Is.True);
        }

        [Test]
        public void SetHasShownClockInstructions_UpdatesSettings()
        {
            _viewModel.SetHasShownClockInstructions(true);

            _mockSettingsService.Verify(s => s.SetHasShownClockInstructions(true), Times.Once);
        }

        #endregion

        #region Time Slice Overlap Tests

        [Test]
        public void ExtendTimeSlice_PreventsCrossingHourBoundaryOverlap()
        {
            // Create a slice from 15-25 minutes (3 to 5 o'clock)
            _viewModel.AddGaugeCommand.Execute(new Point(170, 100)); // ~15 minutes
            var firstSlice = _viewModel.TimeSlices.First();
            _viewModel.ExtendTimeSlice(firstSlice.Name, 25, (int)RadialLevel.Inner);

            // Create a slice at 40 minutes (8 o'clock)
            _viewModel.AddGaugeCommand.Execute(new Point(130, 170)); // ~40 minutes
            var secondSlice = _viewModel.TimeSlices.Last();

            // Try to extend backward across hour boundary to 10 minutes (would overlap with first slice)
            var initialDuration = secondSlice.Duration;
            _viewModel.ExtendTimeSlice(secondSlice.Name, 10, (int)RadialLevel.Inner);

            // The slice should not have been extended because it would overlap
            Assert.That(secondSlice.Duration, Is.EqualTo(initialDuration));
        }

        [Test]
        public void IsWithinTimeSlice_HandlesHourBoundaryCrossing()
        {
            // Create a slice that crosses hour boundary (50 minutes to 10 minutes)
            var slice = new TimeSlice(50, 20, (int)RadialLevel.Inner, "TestSlice");

            // Check minutes that should be within the slice
            Assert.That(slice.IsWithinTimeSlice(50, (int)RadialLevel.Inner), Is.True);
            Assert.That(slice.IsWithinTimeSlice(55, (int)RadialLevel.Inner), Is.True);
            Assert.That(slice.IsWithinTimeSlice(0, (int)RadialLevel.Inner), Is.True);
            Assert.That(slice.IsWithinTimeSlice(5, (int)RadialLevel.Inner), Is.True);
            Assert.That(slice.IsWithinTimeSlice(9, (int)RadialLevel.Inner), Is.True);

            // Check minutes that should NOT be within the slice
            Assert.That(slice.IsWithinTimeSlice(10, (int)RadialLevel.Inner), Is.False);
            Assert.That(slice.IsWithinTimeSlice(30, (int)RadialLevel.Inner), Is.False);
            Assert.That(slice.IsWithinTimeSlice(49, (int)RadialLevel.Inner), Is.False);
        }

        [Test]
        public void AddGauge_DoesNotCreateOverlappingSlices()
        {
            // Create first slice
            _viewModel.AddGaugeCommand.Execute(new Point(150, 100)); // ~30 minutes
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1));

            // Try to create another slice at same position
            _viewModel.AddGaugeCommand.Execute(new Point(150, 100));
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1), "Should not create overlapping slice");
        }

        [Test]
        public void AddGauge_DoesNotCreateOverlappingSlicesOnSameRadialLevel()
        {
            // Create first slice at 15 minutes (3 o'clock)
            _viewModel.AddGaugeCommand.Execute(new Point(170, 100));
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1));

            var firstSlice = _viewModel.TimeSlices.First();

            // Try to create another slice at 17 minutes (would overlap with 15-20 slice)
            // Using a point that would be at approximately 17 minutes
            _viewModel.AddGaugeCommand.Execute(new Point(175, 105));

            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1),
                "Should not create slice that would overlap with existing slice");
        }
        [Test]
        public void AddGauge_AllowsCreationOnDifferentRadialLevel()
        {
            // Create slice in inner ring (distance > 55)
            _viewModel.AddGaugeCommand.Execute(new Point(170, 100)); // Inner ring
            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(1));

            // Create slice in outer ring (distance < 55) - closer to center
            _viewModel.AddGaugeCommand.Execute(new Point(130, 100)); // Outer ring

            Assert.That(_viewModel.TimeSlices.Count, Is.EqualTo(2),
                "Should allow creation on different radial level at same time");

            Assert.That(_viewModel.TimeSlices[0].RadialLevel,
                Is.Not.EqualTo(_viewModel.TimeSlices[1].RadialLevel),
                "Slices should be on different radial levels");
        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_CallsAllowSleep()
        {
            _viewModel.Dispose();

            _mockSleepPreventer.Verify(s => s.AllowSleep(), Times.Once);
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            _viewModel.Dispose();
            _viewModel.Dispose();

            // Should only call AllowSleep once
            _mockSleepPreventer.Verify(s => s.AllowSleep(), Times.Once);
        }

        #endregion

        #region Helper Methods

        // Helper method that mirrors the ViewModel's coordinate calculation
        private static int[] GetMinutesFromCoordinate(Point point)
        {
            const int ClockCenter = 100;

            // Calculate angle from clock center
            var angle = Math.Atan2(point.Y - ClockCenter, point.X - ClockCenter) * (180 / Math.PI);
            angle = 180 + angle;

            // Convert to minutes
            var minutes = (int)(angle / 6);
            minutes = (minutes + 45) % 60;

            // Determine radial level (inner or outer)
            var distance = Math.Sqrt(Math.Pow(point.X - ClockCenter, 2) + Math.Pow(point.Y - ClockCenter, 2));
            int radialLevel = distance < 55 ? (int)RadialLevel.Outer : (int)RadialLevel.Inner;

            return new int[] { minutes, radialLevel };
        }

        #endregion
    }
}