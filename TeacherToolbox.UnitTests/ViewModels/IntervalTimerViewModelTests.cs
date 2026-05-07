using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;

namespace TeacherToolbox.UnitTests.ViewModels
{
    [TestFixture]
    public class IntervalTimerViewModelTests
    {
        [Test]
        public void Constructor_InitializesIntervalNumber()
        {
            var viewModel = new IntervalTimeViewModel(3);

            Assert.That(viewModel.IntervalNumber, Is.EqualTo(3));
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void Constructor_SetsShowRemoveButtonFromIntervalNumber(int intervalNumber, bool expected)
        {
            var viewModel = new IntervalTimeViewModel(intervalNumber);

            Assert.That(viewModel.ShowRemoveButton, Is.EqualTo(expected));
        }

        [Test]
        public void Constructor_InitializesHoursList()
        {
            var viewModel = new IntervalTimeViewModel(1);

            Assert.That(viewModel.HoursList, Is.EqualTo(new List<int>(System.Linq.Enumerable.Range(0, 24))));
        }

        [Test]
        public void Constructor_InitializesMinutesList()
        {
            var viewModel = new IntervalTimeViewModel(1);

            Assert.That(viewModel.MinutesList, Is.EqualTo(new List<int>(System.Linq.Enumerable.Range(0, 60))));
        }

        [Test]
        public void Constructor_InitializesSecondsList()
        {
            var viewModel = new IntervalTimeViewModel(1);

            Assert.That(viewModel.SecondsList, Is.EqualTo(new List<int>(System.Linq.Enumerable.Range(0, 60))));
        }

        [Test]
        public void Hours_WhenChanged_RaisesPropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var propertyName = "";
            viewModel.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            viewModel.Hours = 2;

            Assert.That(propertyName, Is.EqualTo(nameof(IntervalTimeViewModel.Hours)));
        }

        [Test]
        public void Minutes_WhenChanged_RaisesPropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var propertyName = "";
            viewModel.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            viewModel.Minutes = 15;

            Assert.That(propertyName, Is.EqualTo(nameof(IntervalTimeViewModel.Minutes)));
        }

        [Test]
        public void Seconds_WhenChanged_RaisesPropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var propertyName = "";
            viewModel.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            viewModel.Seconds = 45;

            Assert.That(propertyName, Is.EqualTo(nameof(IntervalTimeViewModel.Seconds)));
        }

        [Test]
        public void IntervalNumber_WhenChanged_RaisesPropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var propertyName = "";
            viewModel.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            viewModel.IntervalNumber = 2;

            Assert.That(propertyName, Is.EqualTo(nameof(IntervalTimeViewModel.IntervalNumber)));
        }

        [Test]
        public void ShowRemoveButton_WhenChanged_RaisesPropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var propertyName = "";
            viewModel.PropertyChanged += (_, e) => propertyName = e.PropertyName;

            viewModel.ShowRemoveButton = true;

            Assert.That(propertyName, Is.EqualTo(nameof(IntervalTimeViewModel.ShowRemoveButton)));
        }

        [Test]
        public void SettingSameValue_DoesNotRaisePropertyChanged()
        {
            var viewModel = new IntervalTimeViewModel(1);
            var eventRaised = false;
            viewModel.PropertyChanged += (_, _) => eventRaised = true;

            viewModel.Hours = 0;
            viewModel.Minutes = 0;
            viewModel.Seconds = 0;
            viewModel.IntervalNumber = 1;
            viewModel.ShowRemoveButton = false;

            Assert.That(eventRaised, Is.False);
        }

        [Test]
        public void TotalSeconds_ReturnsCombinedDuration()
        {
            var viewModel = new IntervalTimeViewModel(1)
            {
                Hours = 1,
                Minutes = 2,
                Seconds = 3
            };

            Assert.That(viewModel.TotalSeconds, Is.EqualTo(3723));
        }
    }
}
