using TeacherToolbox.Services;

namespace TeacherToolbox.UnitTests.Services;

[TestFixture]
public class ShortcutWatcherManagerTests
{
    [TestCase("D0", 0)]
    [TestCase("D1", 1)]
    [TestCase("D9", 9)]
    public void TryParseShortcutMessage_TimerShortcut_ReturnsTimerEvent(string message, int expectedNumber)
    {
        var parsed = ShortcutWatcherManager.TryParseShortcutMessage(message, out var args);

        Assert.That(parsed, Is.True);
        Assert.That(args.Kind, Is.EqualTo(ShortcutKind.Timer));
        Assert.That(args.Number, Is.EqualTo(expectedNumber));
        Assert.That(args.RawMessage, Is.EqualTo(message));
    }

    [Test]
    public void TryParseShortcutMessage_F9_ReturnsRandomNameEvent()
    {
        var parsed = ShortcutWatcherManager.TryParseShortcutMessage("F9", out var args);

        Assert.That(parsed, Is.True);
        Assert.That(args.Kind, Is.EqualTo(ShortcutKind.RandomName));
        Assert.That(args.Number, Is.EqualTo(-1));
        Assert.That(args.RawMessage, Is.EqualTo("F9"));
    }

    [TestCase("ALIVE")]
    [TestCase("STARTUP:1234")]
    [TestCase("D10")]
    [TestCase("D")]
    [TestCase("")]
    public void TryParseShortcutMessage_NonShortcutMessage_ReturnsFalse(string message)
    {
        var parsed = ShortcutWatcherManager.TryParseShortcutMessage(message, out var args);

        Assert.That(parsed, Is.False);
        Assert.That(args, Is.Null);
    }
}
