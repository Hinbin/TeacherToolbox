using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using TeacherToolbox.Services;

namespace TeacherToolbox.UnitTests.Services;

[TestFixture]
public class SettingsServiceFactoryTests
{
    [Test]
    public void CreateSync_WhenCalledRepeatedly_ReturnsSameInstance()
    {
        var settingsService = CreateSettingsService();
        var factory = new SettingsServiceFactory(() => settingsService.Object);

        var first = factory.CreateSync();
        var second = factory.CreateSync();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public async Task CreateAsync_WhenCalledRepeatedly_ReturnsSameInstance()
    {
        var settingsService = CreateSettingsService();
        var factory = new SettingsServiceFactory(() => settingsService.Object);

        var first = await factory.CreateAsync();
        var second = await factory.CreateAsync();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public async Task CreateSyncThenCreateAsync_ReturnsSameInstance()
    {
        var settingsService = CreateSettingsService();
        var factory = new SettingsServiceFactory(() => settingsService.Object);

        var syncInstance = factory.CreateSync();
        var asyncInstance = await factory.CreateAsync();

        Assert.That(asyncInstance, Is.SameAs(syncInstance));
    }

    [Test]
    public async Task CreateAsyncThenCreateSync_ReturnsSameInstance()
    {
        var settingsService = CreateSettingsService();
        var factory = new SettingsServiceFactory(() => settingsService.Object);

        var asyncInstance = await factory.CreateAsync();
        var syncInstance = factory.CreateSync();

        Assert.That(syncInstance, Is.SameAs(asyncInstance));
    }

    private static Mock<ISettingsService> CreateSettingsService()
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.LoadSettings()).Returns(Task.CompletedTask);
        return settingsService;
    }
}
