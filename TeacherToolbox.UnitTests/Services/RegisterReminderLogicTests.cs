using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeacherToolbox.Model;
using TeacherToolbox.Services;

namespace TeacherToolbox.UnitTests.Services;

[TestFixture]
public class RegisterReminderLogicTests
{
    private static RegisterReminder MakeReminder(int hour, int minute, bool enabled = true, string label = "") =>
        new() { Id = Guid.NewGuid(), Hour = hour, Minute = minute, IsEnabled = enabled, Label = label };

    private static RegisterReminderSettings MakeSettings(
        bool masterEnabled = true,
        bool weekdaysOnly = false,
        int snoozeMinutes = 3,
        List<RegisterReminder> reminders = null) =>
        new()
        {
            MasterEnabled = masterEnabled,
            WeekdaysOnly = weekdaysOnly,
            SnoozeMinutes = snoozeMinutes,
            Reminders = reminders ?? new List<RegisterReminder>()
        };

    // ── GetDueReminders ──────────────────────────────────────────────────────

    [Test]
    public void GetDueReminders_WhenMasterDisabled_ReturnsEmpty()
    {
        var now = new DateTime(2025, 5, 7, 9, 0, 0); // Wednesday
        var settings = MakeSettings(masterEnabled: false,
            reminders: new List<RegisterReminder> { MakeReminder(9, 0) });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetDueReminders_AtExactSlotTime_ReturnsDueReminder()
    {
        var now = new DateTime(2025, 5, 7, 9, 0, 0); // Wednesday
        var reminder = MakeReminder(9, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(reminder.Id));
    }

    [Test]
    public void GetDueReminders_Within90SecondsBeforeSlot_ReturnsDueReminder()
    {
        var now = new DateTime(2025, 5, 7, 8, 58, 31); // 89 s before 09:00
        var reminder = MakeReminder(9, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetDueReminders_MoreThan90SecondsBeforeSlot_ReturnsEmpty()
    {
        var now = new DateTime(2025, 5, 7, 8, 57, 29); // 91 s before 09:00
        var reminder = MakeReminder(9, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetDueReminders_AlreadyFiredToday_ReturnsEmpty()
    {
        var now = new DateTime(2025, 5, 7, 9, 0, 0);
        var reminder = MakeReminder(9, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });
        var firedToday = new HashSet<string>
        {
            RegisterReminderLogic.MakeFiredKey(reminder.Id, now)
        };

        var result = RegisterReminderLogic.GetDueReminders(now, settings, firedToday);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetDueReminders_DisabledSlot_ReturnsEmpty()
    {
        var now = new DateTime(2025, 5, 7, 9, 0, 0);
        var reminder = MakeReminder(9, 0, enabled: false);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetDueReminders_MultipleSlots_OnlyDueOneReturned()
    {
        var now = new DateTime(2025, 5, 7, 11, 0, 0);
        var r1 = MakeReminder(9, 0);  // past
        var r2 = MakeReminder(11, 0); // due now
        var r3 = MakeReminder(14, 0); // future
        var settings = MakeSettings(reminders: new List<RegisterReminder> { r1, r2, r3 });

        var result = RegisterReminderLogic.GetDueReminders(now, settings, new HashSet<string>());

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(r2.Id));
    }

    // ── ComputeDelayToNextDue ────────────────────────────────────────────────

    [Test]
    public void ComputeDelayToNextDue_WhenMasterDisabled_ReturnsMaxValue()
    {
        var now = new DateTime(2025, 5, 7, 8, 0, 0);
        var settings = MakeSettings(masterEnabled: false,
            reminders: new List<RegisterReminder> { MakeReminder(9, 0) });

        var delay = RegisterReminderLogic.ComputeDelayToNextDue(now, settings, new HashSet<string>());

        Assert.That(delay, Is.EqualTo(TimeSpan.MaxValue));
    }

    [Test]
    public void ComputeDelayToNextDue_OneSlotInFuture_ReturnsCorrectDelay()
    {
        var now = new DateTime(2025, 5, 7, 8, 0, 0);
        var reminder = MakeReminder(9, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { reminder });

        var delay = RegisterReminderLogic.ComputeDelayToNextDue(now, settings, new HashSet<string>());

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(1)));
    }

    [Test]
    public void ComputeDelayToNextDue_SlotAlreadyFiredToday_Skipped()
    {
        var now = new DateTime(2025, 5, 7, 9, 30, 0);
        var r1 = MakeReminder(9, 0);
        var r2 = MakeReminder(11, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder> { r1, r2 });
        var firedToday = new HashSet<string>
        {
            RegisterReminderLogic.MakeFiredKey(r1.Id, now)
        };

        var delay = RegisterReminderLogic.ComputeDelayToNextDue(now, settings, firedToday);

        Assert.That(delay, Is.EqualTo(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(30))));
    }

    [Test]
    public void ComputeDelayToNextDue_AllSlotsPast_ReturnsMaxValue()
    {
        var now = new DateTime(2025, 5, 7, 15, 0, 0);
        var settings = MakeSettings(reminders: new List<RegisterReminder>
        {
            MakeReminder(9, 0),
            MakeReminder(11, 0),
            MakeReminder(13, 0)
        });

        var delay = RegisterReminderLogic.ComputeDelayToNextDue(now, settings, new HashSet<string>());

        Assert.That(delay, Is.EqualTo(TimeSpan.MaxValue));
    }

    // ── MakeFiredKey ─────────────────────────────────────────────────────────

    [Test]
    public void MakeFiredKey_SameDateDifferentTime_AreEqual()
    {
        var id = Guid.NewGuid();
        var date1 = new DateTime(2025, 5, 7, 9, 0, 0);
        var date2 = new DateTime(2025, 5, 7, 14, 30, 0);

        Assert.That(
            RegisterReminderLogic.MakeFiredKey(id, date1),
            Is.EqualTo(RegisterReminderLogic.MakeFiredKey(id, date2)));
    }

    [Test]
    public void MakeFiredKey_DifferentDates_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var date1 = new DateTime(2025, 5, 7, 9, 0, 0);
        var date2 = new DateTime(2025, 5, 8, 9, 0, 0);

        Assert.That(
            RegisterReminderLogic.MakeFiredKey(id, date1),
            Is.Not.EqualTo(RegisterReminderLogic.MakeFiredKey(id, date2)));
    }
}
