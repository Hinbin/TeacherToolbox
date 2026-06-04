using System;
using System.Collections.Generic;
using TeacherToolbox.Model;

namespace TeacherToolbox.Services
{
    /// <summary>
    /// Pure, threading-free scheduling logic — separated so it can be unit-tested.
    /// </summary>
    internal static class RegisterReminderLogic
    {
        internal const int DueWindowSeconds = 90;

        internal static string MakeFiredKey(Guid id, DateTime date) =>
            $"{id}:{date:yyyy-MM-dd}";

        internal static bool HasFiredToday(Guid id, DateTime now, HashSet<string> firedToday) =>
            firedToday.Contains(MakeFiredKey(id, now));

        internal static DaySchedule GetScheduleForDate(RegisterReminder reminder, DateTime date) =>
            date.DayOfWeek switch
            {
                DayOfWeek.Monday => reminder.Monday,
                DayOfWeek.Tuesday => reminder.Tuesday,
                DayOfWeek.Wednesday => reminder.Wednesday,
                DayOfWeek.Thursday => reminder.Thursday,
                DayOfWeek.Friday => reminder.Friday,
                _ => null
            };

        internal static bool IsReminderActiveOn(RegisterReminder reminder, DateTime date)
        {
            var schedule = GetScheduleForDate(reminder, date);
            return schedule?.Enabled == true;
        }

        internal static List<RegisterReminder> GetDueReminders(
            DateTime now,
            RegisterReminderSettings settings,
            HashSet<string> firedToday)
        {
            var result = new List<RegisterReminder>();
            if (!settings.MasterEnabled) return result;

            foreach (var r in settings.Reminders)
            {
                var schedule = GetScheduleForDate(r, now);
                if (schedule == null || !schedule.Enabled) continue;
                if (HasFiredToday(r.Id, now, firedToday)) continue;

                var slotTime = now.Date.AddHours(schedule.Hour).AddMinutes(schedule.Minute);
                if (Math.Abs((now - slotTime).TotalSeconds) <= DueWindowSeconds)
                    result.Add(r);
            }
            return result;
        }

        internal static TimeSpan ComputeDelayToNextDue(
            DateTime now,
            RegisterReminderSettings settings,
            HashSet<string> firedToday)
        {
            if (!settings.MasterEnabled) return TimeSpan.MaxValue;

            TimeSpan shortest = TimeSpan.MaxValue;
            foreach (var r in settings.Reminders)
            {
                for (int dayOffset = 0; dayOffset <= 7; dayOffset++)
                {
                    var date = now.Date.AddDays(dayOffset);
                    var schedule = GetScheduleForDate(r, date);
                    if (schedule == null || !schedule.Enabled) continue;

                    var slotTime = date.AddHours(schedule.Hour).AddMinutes(schedule.Minute);
                    if (slotTime <= now) continue;
                    if (firedToday.Contains(MakeFiredKey(r.Id, slotTime))) continue;

                    TimeSpan delay = slotTime - now;
                    if (delay < shortest) shortest = delay;
                    break;
                }
            }
            return shortest;
        }
    }
}
