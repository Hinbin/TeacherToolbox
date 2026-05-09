namespace TeacherToolbox.Services
{
    public interface IAppEdition
    {
        AppFlavor Flavor { get; }
        bool CloudTelemetryEnabled { get; }
        bool RegisterSyncEnabled { get; }
        bool OutwoodOnlyFeaturesEnabled { get; }
    }
}
